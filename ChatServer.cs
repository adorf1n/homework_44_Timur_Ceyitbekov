using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

var server = new Server();
await server.Start();

class Client
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public string UserName { get; private set; }
    protected internal StreamWriter Writer { get; }
    protected internal StreamReader Reader { get; }

    private TcpClient _client;
    private Server _server;

    public Client(TcpClient tcpClient, Server serverObject)
    {
        _client = tcpClient;
        _server = serverObject;
        var stream = _client.GetStream();
        Reader = new StreamReader(stream, Encoding.Unicode);
        Writer = new StreamWriter(stream, Encoding.Unicode);
        _server.AddClient(this);
    }

    public async Task ProcessAsync()
    {
        try
        {
            string? userName;
            do
            {
                userName = await Reader.ReadLineAsync();
                if (_server.IsUserLogged(userName))
                {
                    await Writer.WriteLineAsync("Имя занято");
                    await Writer.FlushAsync();
                }
                else
                {
                    UserName = userName;
                    await Writer.WriteLineAsync("Имя принято");
                    await Writer.FlushAsync();
                }
            } while (UserName == null);

            string? message = $"{UserName} вошел в чат";
            await _server.BroadcastMessageAsync(message, Id);
            Console.WriteLine(message);

            await _server.SendUserListAsync(this);

            while (true)
            {
                message = await Reader.ReadLineAsync();
                if (message == null) continue;

                if (message.StartsWith("private|"))
                {
                    var parts = message.Split("|");
                    var targetUser = parts[1];
                    var privateMessage = parts[2];
                    await _server.SendPrivateMessageAsync(privateMessage, targetUser, Id);
                }
                else
                {
                    string timestamp = DateTime.Now.ToString("HH:mm");
                    message = $"{UserName} ({timestamp}): {message}";
                    Console.WriteLine(message);
                    await _server.BroadcastMessageAsync(message, Id);
                }
            }
        }
        catch
        {
            string message = $"{UserName} покинул чат";
            Console.WriteLine(message);
            await _server.BroadcastMessageAsync(message, Id);
        }
        finally
        {
            _server.RemoveClient(Id);
        }
    }

    protected internal void Close()
    {
        Writer.Close();
        Reader.Close();
        _client.Close();
    }
}

class Server
{
    private TcpListener _tcpListener = new TcpListener(IPAddress.Any, 11000);
    private Dictionary<string, Client> _clients = new Dictionary<string, Client>();

    protected internal async Task BroadcastMessageAsync(string message, string id)
    {
        foreach (var (_, client) in _clients)
        {
            if (client.Id != id)
            {
                await client.Writer.WriteLineAsync(message);
                await client.Writer.FlushAsync();
            }
        }
    }

    protected internal async Task SendPrivateMessageAsync(string message, string targetUser, string senderId)
    {
        var targetClient = _clients.Values.FirstOrDefault(c => c.UserName == targetUser);
        if (targetClient != null)
        {
            string timestamp = DateTime.Now.ToString("HH:mm");
            await targetClient.Writer.WriteLineAsync($"{_clients[senderId].UserName} ({timestamp}): {message}");
            await targetClient.Writer.FlushAsync();
        }
        else
        {
            await _clients[senderId].Writer.WriteLineAsync($"Пользователь {targetUser} не найден.");
            await _clients[senderId].Writer.FlushAsync();
        }
    }

    protected internal void AddClient(Client client)
    {
        _clients.Add(client.Id, client);
    }

    protected internal void RemoveClient(string id)
    {
        if (_clients.Remove(id, out var client))
        {
            client.Close();
        }
    }

    protected internal async Task SendUserListAsync(Client client)
    {
        var userList = _clients.Values.Select(c => c.UserName).ToList();
        await client.Writer.WriteLineAsync("Активные пользователи: " + string.Join(", ", userList));
        await client.Writer.FlushAsync();
    }

    protected internal bool IsUserLogged(string userName)
    {
        return _clients.Values.Any(c => c.UserName == userName);
    }

    protected internal async Task Start()
    {
        try
        {
            _tcpListener.Start();
            Console.WriteLine("Сервер запущен. Ожидание подключений...");

            while (true)
            {
                TcpClient tcpClient = await _tcpListener.AcceptTcpClientAsync();
                Client clientObject = new Client(tcpClient, this);
                Task.Run(clientObject.ProcessAsync);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            DisconnectAll();
        }
    }

    protected internal void DisconnectAll()
    {
        foreach (var (_, client) in _clients)
        {
            client.Close();
        }
        _tcpListener.Stop();
    }
}

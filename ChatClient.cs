using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

string ip;
int port;
TcpClient client = null;
StreamWriter writer = null;
StreamReader reader = null;

bool connected = false;
while (!connected)
{
    try
    {
        Console.WriteLine("Введите IP сервера:");
        ip = Console.ReadLine();

        Console.WriteLine("Введите порт сервера:");
        port = int.Parse(Console.ReadLine());

        client = new TcpClient(ip, port);
        var stream = client.GetStream();
        reader = new StreamReader(stream, Encoding.Unicode);
        writer = new StreamWriter(stream, Encoding.Unicode);
        connected = true;
    }
    catch
    {
        Console.WriteLine("Не удалось подключиться. Попробовать снова? (Y/N)");
        if (Console.ReadLine().ToUpper() != "Y") return;
    }
}

string userName = null;
do
{
    Console.WriteLine("Введите имя пользователя:");
    userName = Console.ReadLine();

    await writer.WriteLineAsync(userName);
    await writer.FlushAsync();

    string response = await reader.ReadLineAsync();
    if (response == "Имя занято")
    {
        Console.WriteLine("Имя уже используется. Попробуйте снова.");
    }
} while (userName == null);

string activeUsers = await reader.ReadLineAsync();
Console.WriteLine(activeUsers);

Task.Run(async () =>
{
    while (true)
    {
        try
        {
            string serverMessage = await reader.ReadLineAsync();
            Console.WriteLine(serverMessage);
        }
        catch
        {
            Console.WriteLine("Соединение потеряно.");
            break;
        }
    }
});

while (true)
{
    string message = Console.ReadLine();
    if (message.StartsWith("->"))
    {
        var split = message.Split(":", 2);
        var targets = split[0].Substring(2).Split(",");
        foreach (var target in targets)
        {
            await writer.WriteLineAsync($"private|{target.Trim()}|{split[1].Trim()}");
        }
    }
    else
    {
        await writer.WriteLineAsync(message);
    }
    await writer.FlushAsync();
}

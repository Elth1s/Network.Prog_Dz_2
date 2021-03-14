using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Server
{
    class Program
    {
        static int port = 8080; // порт для приема входящих запросов
        static IPAddress iPAddress = IPAddress.Parse("127.0.0.1"); // Dns.GetHostEntry("localhost").AddressList[1]; //localhost
        static List<UserInfo> users = new List<UserInfo>();
        static void Main(string[] args)
        {
            // получаем адреса для запуска сокета
            IPEndPoint ipPoint = new IPEndPoint(iPAddress, port);
            // создаем сокет
            Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            Semaphore semaphore = new Semaphore(2, 2);

            using (FileStream fs = new FileStream("users.txt", FileMode.Open, FileAccess.Read))
            {
                StreamReader sr = new StreamReader(fs);
                UserInfo newUser = new UserInfo();
                while (!sr.EndOfStream)
                {
                    newUser.Login = sr.ReadLine();
                    newUser.Password = sr.ReadLine();
                    users.Add(new UserInfo() { Login = newUser.Login,Password = newUser.Password });
                }
                sr.Close();
            }

            XmlDocument xdoc = new XmlDocument();
            xdoc.Load("https://api.privatbank.ua/p24api/pubinfo?exchange&coursid=11");
            XmlNodeList xNodelst = xdoc.DocumentElement.SelectNodes("//exchangerates/row");
            Dictionary<string, decimal> currencies = new Dictionary<string, decimal>();
            foreach (XmlNode xNode in xNodelst)
            {
                string name = xNode.SelectSingleNode("exchangerate").SelectSingleNode("@ccy").Value;
                string buy = xNode.SelectSingleNode("exchangerate").SelectSingleNode("@buy").Value;
                if (name == "BTC")
                    currencies.Add("UAH", 1);
                else
                    currencies.Add(name, decimal.Parse(buy, CultureInfo.InvariantCulture));
            }
            int action = 0;
            do
            {
                Console.WriteLine("1.Add User");
                Console.WriteLine("2.Start server");
                Console.WriteLine();
                Console.Write("Select: ");
                action = int.Parse(Console.ReadLine());
                switch (action)
                {
                    case 1:
                        UserInfo newUser = new UserInfo();
                        Console.Write("Enter login: ");
                        newUser.Login = Console.ReadLine();
                        Console.Write("Enter password: ");
                        newUser.Password = Console.ReadLine();
                        users.Add(newUser);
                        using (StreamWriter sw = new StreamWriter("users.txt", true, System.Text.Encoding.Default))
                        {
                            sw.WriteLine(newUser.Login);
                            sw.WriteLine(newUser.Password);
                        }
                        break;
                    case 2:
                        try
                        {
                            // связываем сокет с локальной точкой, по которой будем принимать данные
                            listenSocket.Bind(ipPoint);

                            // начинаем прослушивание
                            listenSocket.Listen(10);

                            Console.WriteLine("Server started! Waiting for connection...");

                            while (true)
                            {
                                Socket handler = listenSocket.Accept();

                                Task.Run(() => ServClient(handler, semaphore, currencies));
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                        break;
                    default:
                        break;
                }
            } while (true);
            
        }

        static void ServClient(Socket client, Semaphore semaphore, Dictionary<string, decimal> currencies)
        {
            if (!semaphore.WaitOne(200))
            {
                client.Send(Encoding.Unicode.GetBytes("Server error!"));
                // закрываем сокет
                client.Shutdown(SocketShutdown.Both);
                client.Close();
                return;
            }
            try
            {
                int count = 0;
                while (true)
                {
                    
                    // получаем сообщение
                    StringBuilder builder = new StringBuilder();
                    int bytes = 0; // количество полученных байтов
                    byte[] data = new byte[256]; // буфер для получаемых данных

                    do
                    {
                        bytes = client.Receive(data);
                        builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
                    }
                    while (client.Available > 0);

                    string[] words = builder.ToString().Split(new char[] { ' '}, StringSplitOptions.RemoveEmptyEntries);

                    if (count >= 5)
                    {
                        data = Encoding.Unicode.GetBytes("Number of requests used");
                        client.Send(data);
                        break;
                    }

                    if (words[0] == "<Login>")
                    {
                        for (int i = 0; i < users.Count; i++)
                        {
                            if (users[i].Login == words[1] && users[i].Password == words[2])
                            {
                                client.Send(Encoding.Unicode.GetBytes("Connected"));
                            }
                        }
                    }
                    else if (words[0] == "<Convert>")
                    {
                        foreach (var item in currencies)
                        {
                            if (words[1] == item.Key)
                            {
                                foreach (var item2 in currencies)
                                {
                                    if(words[2] == item2.Key)
                                    {
                                        decimal result = item.Value / item2.Value;
                                        client.Send(Encoding.Unicode.GetBytes(result.ToString()));
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        client.Send(Encoding.Unicode.GetBytes("Error"));
                    }

                    Console.WriteLine($"{client.RemoteEndPoint} - {builder.ToString()} at {DateTime.Now.ToShortTimeString()}");
                    count++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                // закрываем сокет
                client.Shutdown(SocketShutdown.Both);
                client.Close();

                semaphore.Release();
            }
        }

    }
}

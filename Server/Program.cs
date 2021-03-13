using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
                    users.Add(newUser);
                }
            }


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

                    Task.Run(() => ServClient(handler, semaphore));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static void ServClient(Socket client, Semaphore semaphore)
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

                    if(words[0] == "<Login>")
                    {
                        for (int i = 0; i < users.Count; i++)
                        {
                            if (users[i].Login == words[1] && users[i].Password == words[2])
                            {
                                client.Send(Encoding.Unicode.GetBytes("Connected"));
                            }
                        }
                    }
                    else
                    {
                        client.Send(Encoding.Unicode.GetBytes("Error"));
                    }

                    Console.WriteLine($"{client.RemoteEndPoint} - {builder.ToString()} at {DateTime.Now.ToShortTimeString()}");
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

        static long GetFactorial(long number)
        {
            long result = 1;
            for (int i = 2; i <= number; i++)
            {
                result *= i;
                Thread.Sleep(500);
            }
            return result;
        }
    }
}

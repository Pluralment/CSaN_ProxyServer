using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace ProxyServer
{
    class Program
    {
        static void Main()
        {
            try
            {
                TcpListener Expectant = new TcpListener(IPAddress.Parse("127.0.1.1"), 205);
                Expectant.Start();

                // Прослушка входящих подключений
                while (true)
                {
                    TcpClient Receiving = Expectant.AcceptTcpClient();
                    Task ListenTask = new Task(() => Listen(Receiving));
                    ListenTask.Start();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }
        }

        public static byte[] CutPath(byte[] data)
        {
            string buffer = Encoding.UTF8.GetString(data);
            Regex headerRegex = new Regex(@"http:\/\/[a-z0-9а-яё\:\.]*");
            MatchCollection headers = headerRegex.Matches(buffer);
            buffer = buffer.Replace(headers[0].Value, "");
            data = Encoding.UTF8.GetBytes(buffer);
            return data;
        }

        public static void ProcessRequest(byte[] buf, int bufLength, NetworkStream browserStream)
        {
            try
            {
                char[] splitCharsArray = new char[]{ '\r', '\n' };
                string[] buffer = Encoding.UTF8.GetString(buf).Trim().Split(splitCharsArray);
                string host = buffer.FirstOrDefault(x => x.Contains("Host"));
                host = host.Substring(host.IndexOf(":") + 2);
                string[] port = host.Trim().Split(new char[] { ':' });

                TcpClient sender;
                string hostname = port[0];
                // Если в массиве есть имя хоста и порт подключения.
                if (port.Length == 2)
                {
                    sender = new TcpClient(hostname, int.Parse(port[1]));
                }
                else
                {
                    sender = new TcpClient(hostname, 80);
                }

                NetworkStream serverStream = sender.GetStream();
                // Отправка запроса серверу.
                serverStream.Write(CutPath(buf), 0, bufLength);

                byte[] answer = new byte[65536];
                // Получение ответа от сервера.
                int length = serverStream.Read(answer, 0, answer.Length);

                string[] head = Encoding.UTF8.GetString(answer).Split(splitCharsArray);
                // Выборка кода ответа.
                string ResponseCode = head[0].Substring(head[0].IndexOf(" ") + 1);
                Console.WriteLine(host + "  " + ResponseCode);

                browserStream.Write(answer, 0, length);
                serverStream.CopyTo(browserStream);

                serverStream.Close();
            }
            catch
            {
                return;
            }
            finally
            {
                browserStream.Close();
            }
        }

        public static void Listen(TcpClient client)
        {
            NetworkStream browserStream = client.GetStream();
            byte[] buf = new byte[65536];           
            while (browserStream.CanRead)
            {
                if (browserStream.DataAvailable)
                {
                    try
                    {
                        int msgLength = browserStream.Read(buf, 0, buf.Length);
                        ProcessRequest(buf, msgLength, browserStream);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error occured: " + ex.Message);
                        return;
                    }
                }
            }           
            client.Close();
        }
    }
}
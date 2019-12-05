using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections;
using System.Threading.Tasks;
using System.IO;

namespace Sockets
{
    public partial class frmMain : Form
    {
        private Socket ClientSock;                      // клиентский сокет
        private TcpListener Listener;                   // сокет сервера
        private List<Thread> Threads = new List<Thread>();      // список потоков приложения (кроме родительского)
        private bool _continue = true;                          // флаг, указывающий продолжается ли работа с сокетами
        private Dictionary<string, string> nicknames;
        // конструктор формы
        public frmMain()
        {
            InitializeComponent();

            IPHostEntry hostEntry = Dns.GetHostEntry(Dns.GetHostName());    // информация об IP-адресах и имени машины, на которой запущено приложение
            IPAddress IP = hostEntry.AddressList[0];                        // IP-адрес, который будет указан при создании сокета
            int Port = 1010;                                                // порт, который будет указан при создании сокета
            nicknames = new Dictionary<string, string>();

            // определяем IP-адрес машины в формате IPv4
            foreach (IPAddress address in hostEntry.AddressList)
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    IP = address;
                    break;
                }

            // вывод IP-адреса машины и номера порта в заголовок формы, чтобы можно было его использовать для ввода имени в форме клиента, запущенного на другом вычислительном узле
            this.Text += "     " + IP.ToString() + "  :  " + Port.ToString();

            // создаем серверный сокет (Listener для приема заявок от клиентских сокетов)
            Listener = new TcpListener(IP, Port);
            Listener.Start();

            // создаем и запускаем поток, выполняющий обслуживание серверного сокета
            Threads.Clear();
            Threads.Add(new Thread(ReceiveMessage));
            Threads[Threads.Count-1].Start();
        }

        // работа с клиентскими сокетами
        private void ReceiveMessage()
        {
            // входим в бесконечный цикл для работы с клиентскими сокетом
            while (_continue)
            {
                ClientSock = Listener.AcceptSocket();           // получаем ссылку на очередной клиентский сокет
                Threads.Add(new Thread(ReadMessages));          // создаем и запускаем поток, обслуживающий конкретный клиентский сокет
                Threads[Threads.Count - 1].Start(ClientSock);
            }
        }

        // получение сообщений от конкретного клиента
        private void ReadMessages(object ClientSock)
        {
            string msg = "";        // полученное сообщение

            // входим в бесконечный цикл для работы с клиентским сокетом
            while (_continue)
            {
                byte[] buff = new byte[1024];                           // буфер прочитанных из сокета байтов
                ((Socket)ClientSock).Receive(buff);                     // получаем последовательность байтов из сокета в буфер buff
                msg = System.Text.Encoding.Unicode.GetString(buff);     // выполняем преобразование байтов в последовательность символов
                if (msg.Contains(" <<"))
                {
                    int idx = msg.IndexOf(" <<");
                    string host = msg.Substring(0, idx);
                    string nickname = msg.Substring(idx + 4).Replace("\0", "");
                    nicknames[host] = nickname;
                }
                else if (msg.Contains(" >>"))
                {
                    int idx = msg.IndexOf(" >>");
                    string host = msg.Substring(0, idx);
                    string nickname = nicknames[host];
                    msg = nickname + msg.Substring(idx);
                    rtbMessages.Invoke((MethodInvoker)delegate
                    {
                        if (msg.Replace("\0", "") != "")
                        {
                            rtbMessages.Text += "\n >> " + msg;             // выводим полученное сообщение на форму
                            send_all(msg);
                        }
                    });
                }
                Thread.Sleep(500);
            }
        }

        private void send_all(string msg)
        {
            Parallel.ForEach(nicknames.Keys, (x => send(msg, x)));
        }

        private void send(string msg, string ip)
        {
            int Port = 1011;                                // номер порта, через который выполняется обмен сообщениями
            IPAddress IP = IPAddress.Parse(ip);      // разбор IP-адреса сервера, указанного в поле tbIP
            TcpClient Client = new TcpClient();
            Client.Connect(IP, Port);                       // подключение к клиентскому сокету
            byte[] buff = Encoding.Unicode.GetBytes(msg);   // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт
            Stream stm = Client.GetStream();                                                    // получаем файловый поток клиентского сокета
            stm.Write(buff, 0, buff.Length);                                                    // выполняем запись последовательности байт
        }


        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            _continue = false;      // сообщаем, что работа с сокетами завершена
            
            // завершаем все потоки
            foreach (Thread t in Threads)
            {
                t.Abort();
                t.Join(500);
            }

            // закрываем клиентский сокет
            if (ClientSock != null)
                ClientSock.Close();

            // приостанавливаем "прослушивание" серверного сокета
            if (Listener != null)
                Listener.Stop();
        }
    }
}
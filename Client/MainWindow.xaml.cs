using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static int port = 8080; 
        static IPAddress iPAddress = IPAddress.Parse("127.0.0.1"); 
        IPEndPoint ipPoint = new IPEndPoint(iPAddress, port);
        Socket socket = null;
        bool isConnectedUser = false;

        string selectedRadioButtonFirstGroup = "";
        string selectedRadioButtonSecondGroup = "";
        public MainWindow()
        {
            InitializeComponent();
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // подключаемся к удаленному хосту
            socket.Connect(ipPoint);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button clicked = sender as Button;
            string text = $"<{clicked.Content}>";

            if (text == "<Login>" && isConnectedUser == false)
            {
                text += $" {LoginTextBox.Text} {PasswordTextBox.Text}";
            }
            else if (text == "<Convert>" && isConnectedUser == true)
            {
                text += $" {selectedRadioButtonFirstGroup} {selectedRadioButtonSecondGroup}";
            }
            else
            {
                return;
            }
            Task.Run(() =>
            {
                try
                {
                    byte[] data = Encoding.Unicode.GetBytes(text);
                    socket.Send(data);

                    // получаем ответ
                    data = new byte[256]; // буфер для ответа
                    StringBuilder builder = new StringBuilder();
                    int bytes = 0; // количество полученных байт

                    do
                    {
                        bytes = socket.Receive(data, data.Length, 0);
                        builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
                    }
                    while (socket.Available > 0);
                    if (builder.ToString() == "Connected")
                        isConnectedUser = true;
                    Application.Current.Dispatcher.Invoke(() => label.Content = builder.ToString());
                    
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            });
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            socket?.Shutdown(SocketShutdown.Both);
            socket?.Close();
        }

        private void FirstGroupRadioButton_Click(object sender, RoutedEventArgs e)
        {
            selectedRadioButtonFirstGroup = (sender as RadioButton).Content.ToString();
        }

        private void SecondGroupRadioButton_Click(object sender, RoutedEventArgs e)
        {
            selectedRadioButtonSecondGroup = (sender as RadioButton).Content.ToString();
        }
    }
}

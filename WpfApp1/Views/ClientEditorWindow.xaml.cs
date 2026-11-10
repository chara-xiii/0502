using System;
using System.Windows;
using WpfApp1.Data;
using WpfApp1.Models;
using WpfApp1.Services;

namespace WpfApp1.Views
{
    public partial class ClientEditorWindow : Window
    {
        private readonly Client _editing;
        public Client SavedClient { get; private set; }

        public ClientEditorWindow(Client editing)
        {
            InitializeComponent();
            _editing = editing;
            if (_editing != null)
            {
                TitleText.Text = "Редактирование клиента";
                FullNameBox.Text = _editing.FullName;
                PhoneBox.Text = _editing.Phone;
                AddressBox.Text = _editing.Address;
                EmailBox.Text = _editing.Email;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            var fullName = FullNameBox.Text.Trim();
            var phone = ValidationService.NormalizePhone(PhoneBox.Text);
            var address = AddressBox.Text.Trim();
            var email = EmailBox.Text.Trim();

            var error = ValidationService.ValidateClient(fullName, phone, address, email);
            if (error != null)
            {
                ErrorText.Text = error;
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                var client = new Client
                {
                    ClientId = _editing == null ? 0 : _editing.ClientId,
                    FullName = fullName,
                    Phone = phone,
                    Address = address,
                    Email = email
                };

                if (_editing == null)
                {
                    client.ClientId = ClientsRepository.Insert(client);
                }
                else
                {
                    ClientsRepository.Update(client);
                }

                SavedClient = client;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                ErrorText.Text = "Не удалось сохранить клиента: " + ex.Message;
                ErrorText.Visibility = Visibility.Visible;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}

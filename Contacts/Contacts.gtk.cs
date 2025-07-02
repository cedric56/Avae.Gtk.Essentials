using Avae.Gtk.Essentials;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Microsoft.Maui.Devices;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Tmds.DBus.Protocol;
using Tmds.DBus.SourceGenerator;
using VCardParser.Helpers;

namespace Microsoft.Maui.ApplicationModel.Communication
{
    public interface IAccountPicker
    {
        Task<string?> PickAccountAsync(IEnumerable<string> accounts);
        Task<Contact?> PickContactAsync(IEnumerable<Contact> contacts);
    }

    class AccountPickerImplementation : IAccountPicker
    {
        public Task<string?> PickAccountAsync(IEnumerable<string> accounts)
        {
            var window = new ContactsImplementation.ItemSelectionWindow(accounts);
            return window.ShowDialog<string?>((Avalonia.Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).MainWindow);
        }
        public Task<Contact?> PickContactAsync(IEnumerable<Contact> contacts)
        {
            var window = new ContactsImplementation.ItemSelectionWindow(contacts);
            return window.ShowDialog<Contact?>((Avalonia.Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).MainWindow);
        }
    }

    class ContactsImplementation : IContacts
    {
        IAccountPicker AccountPicker { get; }

        public ContactsImplementation(IAccountPicker? accountPicker = null)
        {
            AccountPicker = accountPicker ?? new AccountPickerImplementation();
        }

        public async Task<IEnumerable<Contact>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            using var connection = new Connection(Address.Session);

            try
            {
                await connection.ConnectAsync();
                var activatables = await connection.ListActivatableServicesAsync();

                if (DeviceInfo.Current is DeviceInfoImplementation implementation && implementation.Distribution == Distribution.Ubuntu)
                {
                    if (Process.GetProcessesByName("evolution").Length == 0)
                    {
                        // If Evolution is not running, we need to start it
                        ExecuteBashCommand("E_BOOK_DEBUG=1 evolution");
                    }

                    var activatable = activatables.FirstOrDefault(a => a.Contains("org.gnome.evolution.dataserver.Source"));

                    var uids = new Dictionary<string, string>();
                    var objectManager = new OrgFreedesktopDBusObjectManagerProxy(connection, activatable, "/org/gnome/evolution/dataserver/SourceManager");
                    var sources = await objectManager.GetManagedObjectsAsync();
                    foreach (var entry in sources)
                    {
                        var objectPath = entry.Key;
                        var interfaces = entry.Value;

                        if (interfaces.ContainsKey("org.gnome.evolution.dataserver.Source"))
                        {
                            var source = new OrgGnomeEvolutionDataserverSourceProxy(connection, activatable, objectPath);

                            try
                            {
                                var uid = await source.GetUIDPropertyAsync();                                

                                var data = await source.GetDataPropertyAsync();
                                if (data.Contains("[Address Book]"))
                                {
                                    var displayName = GetDisplayName(data);                                    
                                    uids.Add(uid, displayName);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to get UID for {objectPath}: {ex.Message}");
                            }
                        }
                    }


                    activatable = activatables.FirstOrDefault(a => a.Contains("org.gnome.evolution.dataserver.AddressBook"));


                    var dictionary = new Dictionary<string, IEnumerable<Contact>>();

                    foreach (var uid in uids)
                    {
                        try
                        {
                            var contacts = await GetContacts(connection, activatable, uid.Key, cancellationToken);
                            dictionary.Add(uid.Value, contacts);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to get contacts: {ex.Message}");
                        }
                    }
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        if (dictionary.Count > 1)
                        {
                            var account = await AccountPicker.PickAccountAsync(dictionary.Keys);
                            if (account != null && dictionary.ContainsKey(account))
                            {
                                return dictionary[account];
                            }
                        }
                        else if (dictionary.Count == 1)
                        {
                            var contact = dictionary.First();
                            return contact.Value;
                        }
                        else
                        {
                            return await GetContacts(connection, activatable, "system-address-book", cancellationToken);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return Enumerable.Empty<Contact>();
        }

        public async Task<Contact?> PickContactAsync()
        {
            var contacts = await GetAllAsync();
            if (contacts.Any())
                return await AccountPicker.PickContactAsync(contacts);
            return null!;
        }

        private async Task<IEnumerable<Contact>> GetContacts(Connection connection, string activatable, string uid, CancellationToken cancellation)
        {
            while (!cancellation.IsCancellationRequested)
            {
                try
                {
                    var factory = new OrgGnomeEvolutionDataserverAddressBookFactoryProxy(connection,
                                activatable,
                                "/org/gnome/evolution/dataserver/AddressBookFactory");

                    var token = new CancellationTokenSource();
                    token.CancelAfter(TimeSpan.FromSeconds(1));
                    var book = await factory.OpenAddressBookAsync(uid).WaitAsync(token.Token);

                    var contactsService = new OrgGnomeEvolutionDataserverAddressBookProxy(connection, activatable, book.ObjectPath);
                    token = new CancellationTokenSource();
                    token.CancelAfter(TimeSpan.FromSeconds(2));
                    var list = await contactsService.GetContactListAsync("").WaitAsync(token.Token);
                    var vcards = new List<VCardParser.Models.Contact>();
                    foreach (var contact in list)
                    {
                        vcards.Add(contact.DecodeVCard());
                    }
                    return vcards.Select(c =>
                    {
                        return new Contact(c.Title, "", c.FormattedName, "", c.LastName, "",
                            c.Phones.Select(p => new ContactPhone(p.Number)),
                            c.Emails.Select(e => new ContactEmail(e.Address)),
                            c.FormattedName);
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to get book: {ex.Message}");
                    await Task.Delay(1000); // Wait before retrying
                }
            }
            return Enumerable.Empty<Contact>();
        }

        static void ExecuteBashCommand(string command)
        {
            // according to: https://stackoverflow.com/a/15262019/637142
            // thans to this we will pass everything as one command
            command = command.Replace("\"", "\"\"");

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"" + command + "\"",
                    //UseShellExecute = false,
                    //RedirectStandardOutput = true,
                    //CreateNoWindow = true
                }
            };
            proc.Start();
        }

        static string GetDisplayName(string configData)
        {
            // Regular expression to match DisplayName
            string pattern = @"DisplayName=([^\n\r]+)";
            Match match = Regex.Match(configData, pattern);

            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            return null!; // Return null if DisplayName is not found
        }

        public class ItemSelectionWindow : AnimatableWindow
        {
            public ItemSelectionWindow(IEnumerable<string> addresses)
                : base()
            {
                Title = "Select an account";
                Width = 400;
                Height = 300;

                var listBox = new Avalonia.Controls.ListBox
                {
                    ItemsSource = addresses,
                };

                var okButton = new Avalonia.Controls.Button
                {
                    Content = "OK",
                    Margin = new Thickness(0, 0, 5, 0)
                };
                okButton.Click += (_, _) => Close(listBox.SelectedItem);

                var cancelButton = new Avalonia.Controls.Button
                {
                    Content = "Cancel"
                };
                cancelButton.Click += (_, _) => Close();

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { okButton, cancelButton }
                };

                var mainPanel = new Grid
                {
                    RowDefinitions = new RowDefinitions("*, Auto"),
                    ColumnDefinitions = new ColumnDefinitions("*")
                };
                Grid.SetRow(listBox, 0);
                Grid.SetRow(buttonPanel, 1);
                mainPanel.Children.Add(listBox);
                mainPanel.Children.Add(buttonPanel);

                Content = mainPanel;
            }

            public ItemSelectionWindow(IEnumerable<Contact> contacts)
                : base()
            {
                Title = "Select a contact";
                Width = 400;
                Height = 300;

                var listBox = new Avalonia.Controls.ListBox
                {
                    ItemsSource = contacts,
                };

                var okButton = new Avalonia.Controls.Button
                {
                    Content = "OK",
                    Margin = new Thickness(0, 0, 5, 0)
                };
                okButton.Click += (_, _) => Close(listBox.SelectedItem);

                var cancelButton = new Avalonia.Controls.Button
                {
                    Content = "Cancel"
                };
                cancelButton.Click += (_, _) => Close();

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { okButton, cancelButton }
                };

                var mainPanel = new Grid
                {
                    RowDefinitions = new RowDefinitions("*, Auto"),
                    ColumnDefinitions = new ColumnDefinitions("*")
                };
                Grid.SetRow(listBox, 0);
                Grid.SetRow(buttonPanel, 1);
                mainPanel.Children.Add(listBox);
                mainPanel.Children.Add(buttonPanel);

                Content = mainPanel;
            }
        }
    }
}

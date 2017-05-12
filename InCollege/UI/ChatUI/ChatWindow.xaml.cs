﻿using InCollege.Core.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace InCollege.Client.UI.ChatUI
{
    public partial class ChatWindow : Window, IUpdatable
    {
        Account Partner { get => (Account)DataContext; set => DataContext = value; }

        public ChatWindow()
        {
            InitializeComponent();

            var cancelToken = new CancellationTokenSource();

            Task.Run(async () =>
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    Message lastMessage;
                    int partnerID = Dispatcher.Invoke(() => Partner?.ID ?? -1);
                    try
                    {
                        if (await NetworkUtils.GetCount<Message>(null, (nameof(Message.ToID), App.Account.ID), (nameof(Message.IsRead), false)) > 0 ||

                        ((lastMessage = ((IList<Message>)MessagesLV?.ItemsSource)?.LastOrDefault()) != null && lastMessage.Sender.ID == App.Account.ID && !lastMessage.IsRead &&
                        ((await NetworkUtils.RequestData<Message>(null, (nameof(Message.FromID), App.Account.ID), (nameof(Message.ToID), partnerID)))?.LastOrDefault()?.IsRead ?? false)))
                            await Dispatcher.Invoke(async () => await UpdateData());
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.ToString());
                    }
                    Thread.Sleep(100);


                    Thread.Sleep(100);
                }
            }, cancelToken.Token);

            Closing += (s, e) =>
            {
                cancelToken.Cancel();
            };
        }

        public async Task UpdateAccounts()
        {
            PeopleLV.ItemsSource = (await NetworkUtils.RequestData<Account>(this)).Where(c => c.ID != App.Account.ID);
        }

        public async Task UpdateData()
        {
            if (PeopleLV.SelectedItem != null)
            {
                if (Partner != null)
                {
                    PeopleLV.IsEnabled = false;

                    var messagesData = await NetworkUtils.RequestData<Message>(this, (nameof(Message.FromID), App.Account.ID),
                                                                             (nameof(Message.ToID), Partner.ID));
                    messagesData.AddRange(await NetworkUtils.RequestData<Message>(this, (nameof(Message.ToID), App.Account.ID),
                                                                                (nameof(Message.FromID), Partner.ID)));
                    if (messagesData.Count == 0)
                    {
                        MessagesLV.Visibility = Visibility.Collapsed;
                        NoMessagesTB.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        messagesData.Sort((m1, m2) => m1.MessageDate < m2.MessageDate ? -1 : 1);
                        messagesData.ForEach(c => { c.Sender = c.FromID == Partner.ID ? Partner : c.FromID == App.Account.ID ? App.Account : null; });
                        MessagesLV.ItemsSource = messagesData;
                        MessagesLV.SelectedIndex = MessagesLV.Items.Count - 1;
                        MessagesLV.ScrollIntoView(MessagesLV.SelectedItem);
                        MessagesLV.Visibility = Visibility.Visible;
                        NoMessagesTB.Visibility = Visibility.Collapsed;
                    }

                    PeopleLV.IsEnabled = true;
                }
                MessageTB.IsEnabled = true;
                SendButton.IsEnabled = true;
            }
            else
            {
                MessageTB.IsEnabled = false;
                SendButton.IsEnabled = false;
            }
        }

        async void ChatWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await UpdateAccounts();
            await UpdateData();
        }

        async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await NetworkUtils.ExecuteDataAction<Message>(this, new Message
            {
                Sender = App.Account,
                Receiver = Partner,
                MessageDate = DateTime.Now,
                MessageText = MessageTB.Text
            }, DataAction.Save);
            MessageTB.Text = string.Empty;
        }

        async void PeopleLV_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Partner = (Account)PeopleLV.SelectedItem;
            await UpdateData();
        }

        void SearchTB_TextChanged(object sender, TextChangedEventArgs e)
        {
            PeopleLV.Items.Filter = c => ((Account)c).FullName.ToLower().Contains(SearchTB.Text.ToLower());
        }
    }
}
﻿using InCollege.Core.Data;
using InCollege.Core.Data.Base;
using MaterialDesignThemes.Wpf;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace InCollege.Client.UI.AccountsUI
{
    public partial class AccountEditDialog : DialogHost
    {
        public static DependencyProperty ShowAccountChangeAlertProperty =
            DependencyProperty.Register("ShowAccountChangeAlert", typeof(Account), typeof(AccountEditDialog));
        public bool ShowAccountChangeAlert { get; set; }

        public static readonly RoutedEvent OnSaveEvent = EventManager.RegisterRoutedEvent("OnSave", RoutingStrategy.Direct, typeof(RoutedEventHandler), typeof(AccountEditDialog));
        public event RoutedEventHandler OnSave
        {
            add => AddHandler(OnSaveEvent, value);
            remove => RemoveHandler(OnSaveEvent, value);
        }

        public static readonly RoutedEvent OnCancelEvent = EventManager.RegisterRoutedEvent("OnCancel", RoutingStrategy.Direct, typeof(RoutedEventHandler), typeof(AccountEditDialog));
        public event RoutedEventHandler OnCancel
        {
            add => AddHandler(OnCancelEvent, value);
            remove => RemoveHandler(OnCancelEvent, value);
        }

        Account AccountDataContext => DataContext as Account;

        public Account Account
        {
            get
            {
                if (AccountDataContext != null)
                {
                    AccountDataContext.Password = PasswordTB.Password;
                    AccountDataContext.AccountType = (AccountType)AccountTypeCB.SelectedIndex;
                }
                return AccountDataContext;
            }
            set
            {
                if (value != null)
                {
                    if (value == App.Account)
                        UserNameTB.IsEnabled = false;
                    PasswordTB.Password = value.Password;
                    AccountTypeCB.SelectedIndex = (byte)value.AccountType;
                }
                DataContext = value;
            }
        }

        public AccountEditDialog()
        {
            InitializeComponent();
        }

        void AccountTypeCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ShowAccountChangeAlert)
                if ((!(AccountTypeCB.Tag is bool) || (bool)AccountTypeCB.Tag) &&
                    e.RemovedItems.Count > 0 &&
                    MessageBox.Show("Внимание!\n" +
                    "Изменение приведет к невозможности авторизации до подтверждения администратором вашей должности.\n" +
                    "Продолжить?", "Изменение должности", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                {
                    AccountTypeCB.Tag = false;
                    AccountTypeCB.SelectedItem = e.RemovedItems[0];
                    return;
                }
                else
                    AccountTypeCB.Tag = true;
        }

        void ProfileSaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (Account.ID != App.Account.ID || string.IsNullOrWhiteSpace(PasswordTB.Password) ||
                MessageBox.Show("Пароль изменен, вы будете деавторизованы, продолжить?", "Смена пароля", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                AccountDataContext.Modified = true;
                RaiseEvent(new RoutedEventArgs(OnSaveEvent));
            }
            else RaiseEvent(new RoutedEventArgs(OnCancelEvent));
        }

        void ProfileCancelButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(OnCancelEvent));
        }

        void FullNameTB_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Account.IsLocal)
            {
                Account.UserName = string.IsNullOrWhiteSpace(FullNameTB.Text) ?
                                   string.Empty :
                                   FullNameTB.Text.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries)[0];
                BindingOperations.GetBindingExpression(UserNameTB, TextBox.TextProperty).UpdateTarget();
            }
        }
    }
}

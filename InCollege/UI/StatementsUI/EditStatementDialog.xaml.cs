﻿using InCollege.Core.Data;
using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Data;
using System;
using System.Globalization;
using InCollege.Core.Data.Base;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Linq;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace InCollege.Client.UI.StatementsUI
{
    public partial class EditStatementDialog : DialogHost, IUpdatable
    {
        #region Properties
        public event RoutedEventHandler OnSave;
        public event RoutedEventHandler OnCancel;

        public static DependencyProperty StatementProperty = DependencyProperty.Register("Statement", typeof(Statement), typeof(EditStatementDialog));
        public Statement Statement
        {
            get => (Statement)GetValue(StatementProperty);
            set => SetValue(StatementProperty, DataContext = value);
        }

        public bool AddMode { get; set; }
        public bool GeneratedComplexMode { get; set; }
        public bool GeneratedTotalMode { get; set; }
        public List<StatementResult> StatementResults { get; set; }

        private bool DestructionMode { get; set; }
        #endregion

        public EditStatementDialog()
        {
            InitializeComponent();

            for (int i = 1; i <= 12; i++)
                SemesterCB.Items.Add(i);
            for (int i = 1; i <= 6; i++)
                CourseCB.Items.Add(i);
        }

        public async Task Show(Statement statement, bool addMode, bool generatedComplexMode, bool generatedTotalMode)
        {
            DestructionMode = false;
            if (generatedComplexMode && generatedTotalMode)
                MessageBox.Show("Ошибка! Ведомость не может быть сгенерирована одновременно \"Составной\" и \"Сводной\"");
            else
            {
                AddMode = addMode;
                GeneratedComplexMode = generatedComplexMode;
                GeneratedTotalMode = generatedTotalMode;

                Statement = statement;
                if (AddMode)
                    if (GeneratedComplexMode)
                        Statement.StatementType = StatementType.QualificationExam;
                    else if (GeneratedTotalMode)
                        Statement.StatementType = StatementType.Total;

                await UpdateData();

                IsOpen = true;
            }
        }

        #region Updaters
        public void UpdateGroupList()
        {
            if (SpecialtyCB.SelectedItem == null)
                GroupCB.Visibility = Visibility.Collapsed;
            else
            {
                if (GroupCB.Items != null && (GroupCB.SelectedItem == null || ((Group)GroupCB.SelectedItem).SpecialtyID != ((Specialty)SpecialtyCB.SelectedItem).ID))
                    GroupCB.Items.Filter = c => (((Group)c)?.SpecialtyID ?? -1) == (((Specialty)SpecialtyCB.SelectedItem)?.ID ?? -1);
                GroupCB.Visibility = Visibility.Visible;
            }
        }

        public async Task UpdateData()
        {
            if (AddMode)
            {
                StatementResultsLV.ItemsSource = null;
                if (SpecialtyCB.SelectedItem == null)
                    GroupCB.Visibility = Visibility.Collapsed;
            }

            StatementResults = await NetworkUtils.RequestData<StatementResult>(null, (nameof(StatementResult.StatementID), Statement?.ID ?? -1));

            var studentsData = await NetworkUtils.RequestData<Account>(null, (nameof(Account.AccountType), AccountType.Student), (nameof(Account.GroupID), Statement.GroupID));

            foreach (var current in StatementResults)
                current.Student = studentsData.FirstOrDefault(c => c.ID == current.StudentID);

            if (GroupCB.SelectedItem != null)
                StudentCB.ItemsSource = studentsData.Where(currentStudent => !StatementResults.Any(c => c.StudentID == currentStudent.ID)).ToList();

            StatementResultsLV.ItemsSource = StatementResults;

            if (GeneratedComplexMode || GeneratedTotalMode)
            {
                SubjectCB.Visibility = Visibility.Collapsed;
                var binding = BindingOperations.GetBindingExpressionBase(StatementTypeCB, Selector.SelectedValueProperty);
                if (GeneratedComplexMode)
                    MiddleItem.IsEnabled = ExamItem.IsEnabled = TotalItem.IsEnabled = false;
                else if (GeneratedTotalMode)
                    StatementTypeCB.IsEnabled = false;

                SubjectCB.IsEnabled = true;
                GroupCB.IsEnabled = SpecialtyCB.IsEnabled = !GeneratedTotalMode;

                BindingOperations.GetBindingExpressionBase(StatementTypeCB, Selector.SelectedIndexProperty).UpdateTarget();
            }
            else
            {
                StatementTypeCB.IsEnabled = StatementResultsLV.Items.Count == 0;
                GroupCB.IsEnabled = SpecialtyCB.IsEnabled = StatementResultsLV.Items.Count == 0 || AddMode;
                SubjectCB.Visibility = Visibility.Visible;
                foreach (ComboBoxItem current in StatementTypeCB.Items)
                    current.IsEnabled = true;
            }


            bool canContainResults = SpecialtyCB.SelectedItem != null && GroupCB.SelectedItem != null || GeneratedTotalMode;
            StatementResultsContainer.Visibility = canContainResults ? Visibility.Visible : Visibility.Collapsed;
            UnfilledBlankResults.Visibility = canContainResults ? Visibility.Collapsed : Visibility.Visible;
        }
        #endregion
        #region Selection callbacks

        private bool handleGroupSelection = true;
        async void GroupCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (handleGroupSelection && !DestructionMode)
                if (Statement != null && Statement.StatementType != StatementType.Total && AddMode && ClientConfiguration.Instance.AutoFillStudents)
                    if ((StatementResults?.Count ?? 0) == 0 || StatementResults.All(c => c.MarkValue == (sbyte)TechnicalMarkValue.Blank) ||
                        MessageBox.Show("Внимание! Ведомость содержит оценки, смена группы приведет к их удалению. Продолжить?", "Опасная операция", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        GroupCB.IsEnabled = false;
                        await NetworkUtils.RemoveWhere<StatementResult>(this, (nameof(StatementResult.StatementID), Statement.ID));
                        int ticketNumber = 0;
                        bool fillTicketNumbers = ClientConfiguration.Instance.AutoFillTicketNumbers;
                        foreach (var current in StudentCB.ItemsSource)
                            await NetworkUtils.ExecuteDataAction<StatementResult>(null, new StatementResult
                            {
                                StatementID = Statement.ID,
                                MarkValue = (sbyte)TechnicalMarkValue.Blank,
                                Student = (Account)current,
                                Subject = (Subject)SubjectCB.SelectedItem,
                                StatementResultDate = Statement.StatementDate,
                                TicketNumber = fillTicketNumbers ? ++ticketNumber : -1,
                            }, DataAction.Save);
                        GroupCB.IsEnabled = true;
                        await UpdateData();
                    }
                    else
                    {
                        handleGroupSelection = false;
                        GroupCB.SelectedItem = e.RemovedItems[0];
                        return;
                    }
            handleGroupSelection = true;
        }

        async void SubjectCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Statement != null && !DestructionMode)
                if (Statement.StatementType == StatementType.Middle || Statement.StatementType == StatementType.Exam)
                {
                    IsEnabled = false;
                    foreach (var current in StatementResults)
                    {
                        current.Subject = (Subject)SubjectCB.SelectedItem;
                        await NetworkUtils.ExecuteDataAction<StatementResult>(null, current, DataAction.Save);
                    }
                    IsEnabled = true;
                    await UpdateData();
                }
        }

        void SpecialtyCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateGroupList();
            ApplyFilter();
        }

        void StatementTypeCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StatementTypeCB.SelectedIndex <= 1)
            {
                MiddleStatementResultsContainer.Visibility = Visibility.Visible;
                ComplexStatementPanel.Visibility = Visibility.Collapsed;

                if (StatementTypeCB.SelectedIndex == 0)
                {
                    TicketNumberColumnHeader.Visibility = Visibility.Collapsed;
                    TicketNumberColumn.Width = 0;
                    TicketNumberTB.Visibility = Visibility.Collapsed;
                }
                else
                {
                    TicketNumberColumnHeader.Visibility = Visibility.Visible;
                    TicketNumberColumn.Width = 120;
                    TicketNumberTB.Visibility = Visibility.Visible;
                }
            }
            else
            {
                MiddleStatementResultsContainer.Visibility = Visibility.Collapsed;
                ComplexStatementPanel.Visibility = Visibility.Visible;
            }
        }

        void SemesterCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Statement != null)
            {
                var optimal = (int)Math.Ceiling((double)Statement.Semester / 2);
                Statement.Course = optimal == 0 ? 1 : optimal;
                BindingOperations.GetBindingExpressionBase(CourseCB, Selector.SelectedValueProperty).UpdateTarget();
                ApplyFilter();
            }
        }

        void CourseCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Statement != null)
            {
                var optimal = Statement.Course * 2;
                if (optimal > Statement.Semester || optimal < Statement.Semester)
                    Statement.Semester = optimal - 1;
                BindingOperations.GetBindingExpressionBase(SemesterCB, Selector.SelectedValueProperty).UpdateTarget();
                ApplyFilter();
            }
        }
        #endregion
        #region Separate windows
        void CommissionMembersButton_Click(object sender, RoutedEventArgs e)
        {
            new StatementCommissionMembersWindow(Statement).ShowDialog();
        }

        void AttestationTypesButton_Click(object sender, RoutedEventArgs e)
        {
            new StatementAttestationTypesWindow(Statement).ShowDialog();
        }

        void SeparateWindowButton_Click(object sender, RoutedEventArgs e)
        {
            new StatementResultsWindow(Statement).ShowDialog();
        }
        #endregion
        #region Middle statement results list
        void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (SubjectCB.SelectedItem != null)
            {
                StatementResultDialog.DataContext = new StatementResult
                {
                    StatementID = Statement.ID,
                    SubjectID = ((Subject)SubjectCB.SelectedItem).ID,
                    StatementResultDate = Statement.StatementDate
                };
                StatementResultDialog.IsOpen = true;
            }
            else MessageBox.Show("Выберите дисциплину!");
        }

        async void RePassesItem_Click(object sender, RoutedEventArgs e)
        {
            if (StatementResultsLV.SelectedItem != null)
            {
                var item = (StatementResult)StatementResultsLV.SelectedItem;
                new RePassesWindow(Statement.ID, item.ID, item.SubjectID, item.StudentFullName, this).ShowDialog();
                await UpdateData();
            }
        }

        void EditStatementResultItem_Click(object sender, RoutedEventArgs e)
        {
            if (StatementResultsLV.SelectedItem != null)
            {
                StudentCB.ItemsSource = new[] { ((StatementResult)StatementResultsLV.SelectedItem).Student };
                StatementResultDialog.DataContext = StatementResultsLV.SelectedItem;
                StatementResultDialog.IsOpen = true;
            }
        }

        async void RemoveStatementResultItem_Click(object sender, RoutedEventArgs e)
        {
            foreach (DBRecord current in StatementResultsLV.SelectedItems)
            {
                await NetworkUtils.ExecuteDataAction<StatementResult>(null, current, DataAction.Remove);
                await NetworkUtils.RemoveWhere<RePass>(null, (nameof(RePass.StatementResultID), current.ID));
            }
            await UpdateData();
        }

        async void SaveStatementResultButton_Click(object sender, RoutedEventArgs e)
        {
            if (StudentCB.SelectedItem != null && MarkCB.SelectedItem != null)
            {
                var statementResult = (StatementResult)StatementResultDialog.DataContext;
                statementResult.MarkValue = MarkCB.SelectedIndex >= 0 && MarkCB.SelectedIndex < 4 ? (sbyte)(MarkCB.SelectedIndex + 2) :
                    (sbyte)(TechnicalMarkValue)Enum.Parse(typeof(TechnicalMarkValue), ((ComboBoxItem)MarkCB.SelectedItem).Name.Split(new[] { "Item" }, StringSplitOptions.RemoveEmptyEntries)[0]);

                await NetworkUtils.ExecuteDataAction<StatementResult>(this, statementResult, DataAction.Save);
            }
            StatementResultDialog.IsOpen = false;
        }

        async void CancelStatementResultButton_Click(object sender, RoutedEventArgs e)
        {
            StatementResultDialog.IsOpen = false;
            await UpdateData();
        }
        #endregion
        #region Text filters
        void StatementNumberTB_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveButton.IsEnabled = !string.IsNullOrWhiteSpace(StatementNumberTB.Text);
        }

        void StatementNumberTB_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            string futureText = StatementNumberTB.Text + e.Text;
            e.Handled = string.IsNullOrWhiteSpace(futureText) || !System.Text.RegularExpressions.Regex.IsMatch(futureText, "^\\d{1,10}$");
        }

        private void TicketNumberTB_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            string futureText = StatementNumberTB.Text + e.Text;
            e.Handled = string.IsNullOrWhiteSpace(futureText) || !System.Text.RegularExpressions.Regex.IsMatch(futureText, "^\\d{1,10}$");
        }

        void StatementNumberTB_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = e.Key == Key.Space;
        }

        private void TicketNumberTB_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = e.Key == Key.Space;
        }
        #endregion
        #region Dialog buttons
        void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            BindingOperations.GetBindingExpressionBase(StatementDatePicker, DatePicker.SelectedDateProperty).UpdateSource();
            if (Statement.StatementDate != null)
            {
                DestructionMode = true;
                OnSave?.Invoke(sender, e);
                IsOpen = false;
            }
            else MessageBox.Show("Укажите дату!");
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DestructionMode = true;
            StatementResultDialog.IsOpen = false;
            OnCancel?.Invoke(sender, e);
        }

        async void SaveDocButton_Click(object sender, RoutedEventArgs e)
        {
            SaveDocButton.IsEnabled = false;

            var templateFile = Path.Combine(CommonVariables.TemplatesDirectory, $"{Statement.StatementType}.docx");

            OpenFileDialog openTemplateDialog = new OpenFileDialog { Filter = "Шаблон в формате Word|*.docx" };
            if (!File.Exists(templateFile))
                if ((Statement.StatementType == StatementType.Other && (openTemplateDialog.ShowDialog() ?? false))
                    ||
                    (Statement.StatementType != StatementType.Other &&
                    MessageBox.Show($"Шаблон \"{Statement.StatementType}\" не найден! У вас есть такой шаблон?", "Шаблон не найден", MessageBoxButton.YesNo) == MessageBoxResult.Yes &&
                    (openTemplateDialog.ShowDialog() ?? false)))

                    templateFile = openTemplateDialog.FileName;

            if (File.Exists(templateFile))
            {
                var saveStatementDialog = new SaveFileDialog
                {
                    AddExtension = true,
                    Filter = "Документ Microsoft Word|*.docx"
                };
                if (saveStatementDialog.ShowDialog() ?? false)
                    try
                    {
                        var attestationTypes = (await NetworkUtils.RequestData<StatementAttestationType>(null, (nameof(StatementAttestationType.StatementID), Statement.ID)))
                                                .Select(async c => (await NetworkUtils.RequestData<AttestationType>(null, (nameof(AttestationType.ID), c.AttestationTypeID)))?.FirstOrDefault());

                        var commissionMembers = (await NetworkUtils.RequestData<CommissionMember>(null, (nameof(CommissionMember.StatementID), Statement.ID)))
                                                .Select(async c => (await NetworkUtils.RequestData<Account>(null, (nameof(Account.ID), c.ProfessorID)))?.FirstOrDefault());

                        var attestationTypesResults = new List<AttestationType>();
                        var commissionMembersResults = new List<Account>();

                        foreach (var current in attestationTypes)
                            attestationTypesResults.Add(await current);

                        foreach (var current in commissionMembers)
                            commissionMembersResults.Add(await current);

                        if (StatementResults != null)
                        {
                            var subjects = (List<Subject>)SubjectCB.ItemsSource;
                            foreach (var current in StatementResults)
                            {
                                var subject = subjects.FirstOrDefault(c => c.ID == current.SubjectID);
                                //Do not use short form. We have to preserve SubjectID
                                if (subject != null)
                                    current.Subject = subject;
                            }
                        }

                        DocumentUtils_DOCX.SaveStatementWithTemplate(Statement, StatementResults,
                            attestationTypesResults?.ToList(),
                            commissionMembersResults?.ToList(),
                            templateFile,
                            saveStatementDialog.FileName);
                    }
                    catch (IOException exc)
                    {
                        MessageBox.Show($"Ошибка сохранения!\nCообщение:\n{exc.Message}");
                    }
            }
            SaveDocButton.IsEnabled = true;
        }
        #endregion

        public void ApplyFilter()
        {
            if (SubjectCB != null)
                SubjectCB.Items.Filter = c =>
                  {
                      var subject = (Subject)c;
                      return (subject.Semester <= 0 || SemesterCB.SelectedItem == null || subject.Semester == (int)SemesterCB.SelectedItem) &&
                             (subject.Specialty == null || SpecialtyCB.SelectedItem == null || subject.Specialty == (Specialty)SpecialtyCB.SelectedItem);
                  };
        }

    }
    #region Converters
    public class IndexConverter : IValueConverter
    {
        public object Convert(object value, Type TargetType, object parameter, CultureInfo culture)
        {
            var item = (ListViewItem)value;
            return ItemsControl.ItemsControlFromItemContainer(item).ItemContainerGenerator.IndexFromContainer(item) + 1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    public class StatementTypeToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (int)(StatementType)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (StatementType)(int)value;
        }
    }

    public class TicketNumberConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (int)value == -1 ? "" : value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return int.TryParse(value.ToString(), out int intValue) ? intValue : -1;
        }
    }

    #endregion
}

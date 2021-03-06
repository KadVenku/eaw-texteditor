﻿using eaw_texteditor.client.ui.dialogs.add;
using eaw_texteditor.client.ui.dialogs.edit;
using eaw_texteditor.client.ui.dialogs.export;
using eaw_texteditor.client.ui.dialogs.load;
using eaw_texteditor.client.ui.dialogs.settings;
using eaw_texteditor.shared.common.util.ui;
using eaw_texteditor.shared.data.main;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using MahApps.Metro.SimpleChildWindow;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using ts.translation;
using ts.translation.common.typedefs;
using ts.translation.common.util.observable;
using ts.translation.data.holder.observables;

namespace eaw_texteditor.client.ui.main
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private MainWindowData FormData { get; set; }
        private DependencyObject CurrentRightClickedObject { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            ImportFormData(new MainWindowData());
        }

        private void ImportFormData(MainWindowData data)
        {
            data.IsKeySearchChecked = true;
            data.UseSimpleSearch = true;
            data.IsMatchCaseChecked = true;
            data.IsTranslationDataLoaded = false;
            FormData = data;
            DataContext = data;
        }

        private void AdvancedSearchCheckBoxCheckedChanged(object sender, RoutedEventArgs e)
        {
            _advancedSearchFormGrid.IsEnabled = FormData.IsAdvancedSearchCheckBoxChecked;
            _advancedSearchFormGrid.Visibility = _advancedSearchFormGrid.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void _settingsExecuteButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow childWindow = new SettingsWindow() { IsModal = true };
            await this.ShowChildWindowAsync<bool>(childWindow, ChildWindowManager.OverlayFillBehavior.FullWindow);
            FormData.SelectedLanguage = childWindow.FormData.SelectedLanguage;
        }

        private async void _basicEditorDataGrid_OnDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                e.Handled = true;
                return;
            }

            DependencyObject source = (DependencyObject)e.OriginalSource;
            DataGridRow row = UiUtility.TryFindParent<DataGridRow>(source);
            if (row == null)
            {
                return;
            }

            if (!(row.Item is ObservableTranslationData translationItem))
            {
                e.Handled = true;
                return;
            }

            ObservableTranslationData english = null;
            ObservableTranslationData german = null;
            ObservableTranslationData french = null;
            ObservableTranslationData italian = null;
            ObservableTranslationData spanish = null;
            PrepareEditWindow(translationItem, ref english, ref french, ref italian, ref german, ref spanish);
            EditTextKeyWindow w = new EditTextKeyWindow(FormData.SelectedLanguage, english, german, french, italian, spanish) { IsModal = true };
            await this.ShowChildWindowAsync<bool>(w, ChildWindowManager.OverlayFillBehavior.FullWindow);
            if (w.FormData.TranslationChanged)
            {
                FormData.IsEdited = true;
            }
            e.Handled = true;
        }

        private async void _importExecuteButton_Click(object sender, RoutedEventArgs e)
        {
            LoadFromFileWindow childWindow = new LoadFromFileWindow() { IsModal = true };
            await this.ShowChildWindowAsync<bool>(childWindow, ChildWindowManager.OverlayFillBehavior.FullWindow);
            if (!childWindow.FormData.ResultOk)
            {
                return;
            }
            IsEnabled = false;
            //_mainBoxTabControl.Visibility = Visibility.Collapsed;
            _mainBoxLoadingControl.Visibility = Visibility.Visible;
            try
            {
                PGTEXTS.LoadFromFile(childWindow.FormData.ImportPath, childWindow.FormData.ImportType);
                ImportFromPgTextSource();
                bool userLangCanBeUsed = false;
                foreach (PGLanguage loadedLanguage in PGTEXTS.GetLoadedLanguages())
                {
                    if (loadedLanguage == Properties.Settings.Default.USR_LOADED_LANGUAGE)
                    {
                        userLangCanBeUsed = true;
                    }
                }
                FormData.SelectedLanguage = userLangCanBeUsed ? Properties.Settings.Default.USR_LOADED_LANGUAGE : PGTEXTS.GetLoadedLanguages().FirstOrDefault();
                FormData.Languages = new ObservableCollection<PGLanguage>(PGTEXTS.GetLoadedLanguages());
                FormData.IsTranslationDataLoaded = true;
            }
            catch (Exception ex)
            {
                if (!IsEnabled)
                {
                    IsEnabled = true;
                    //_mainBoxTabControl.Visibility = Visibility.Visible;
                    _mainBoxLoadingControl.Visibility = Visibility.Collapsed;
                }
                await this.ShowMessageAsync("Warning!", $"Something went wrong.\n{ex}");
            }

            if (IsEnabled)
            {
                return;
            }

            IsEnabled = true;
            //_mainBoxTabControl.Visibility = Visibility.Visible;
            _mainBoxLoadingControl.Visibility = Visibility.Collapsed;
        }

        private void ImportFromPgTextSource()
        {
            foreach (PGLanguage loadedLanguage in PGTEXTS.GetLoadedLanguages())
            {
                if (FormData.Sources.ContainsKey(loadedLanguage))
                {
                    FormData.Sources.Remove(loadedLanguage);
                }

                FormData.Sources.Add(loadedLanguage, new CollectionViewSource() { Source = ObservableTranslationUtility.GetTranslationDataAsObservable(loadedLanguage) });
            }

            FormData.Languages = new ObservableCollection<PGLanguage>(PGTEXTS.GetLoadedLanguages());
        }

        private async void _exportExecuteButton_Click(object sender, RoutedEventArgs e)
        {
            await ExportToFile();
        }

        private async Task ExportToFile()
        {
            ExportToFileWindow childWindow = new ExportToFileWindow() { IsModal = true };
            await this.ShowChildWindowAsync<bool>(childWindow, ChildWindowManager.OverlayFillBehavior.FullWindow);
            if (!childWindow.FormData.ResultOk)
            {
                return;
            }

            if (Directory.EnumerateFileSystemEntries(childWindow.FormData.ExportPath).Any())
            {
                MessageDialogResult dialogResult = await this.ShowMessageAsync("Warning!", $"The export folder is not empty.\nThis may overwrite {(childWindow.FormData.ExportType == TSFileTypes.FileTypeXmlv1 ? "an existing \'TranslationManifest.xml\' file." : "any existing \'MASTERTEXTFILE_{LANGUAGE}.DAT\' files.")}\nCurrently selected directory: {childWindow.FormData.ExportPath}\n\nContinue exporting?", MessageDialogStyle.AffirmativeAndNegative);
                if (dialogResult != MessageDialogResult.Affirmative)
                {
                    return;
                }
            }

            IsEnabled = false;
            //_mainBoxTabControl.Visibility = Visibility.Collapsed;
            _mainBoxLoadingControl.Visibility = Visibility.Visible;
            try
            {
                PGTEXTS.SaveToFile(childWindow.FormData.ExportPath, childWindow.FormData.ExportType);
            }
            catch (Exception ex)
            {
                if (!IsEnabled)
                {
                    IsEnabled = true;
                    //_mainBoxTabControl.Visibility = Visibility.Visible;
                    _mainBoxLoadingControl.Visibility = Visibility.Collapsed;
                }

                await this.ShowMessageAsync("Warning!", $"Something went wrong.\n{ex}");
            }

            FormData.IsEdited = false;
            if (IsEnabled)
            {
                return;
            }

            IsEnabled = true;
            //_mainBoxTabControl.Visibility = Visibility.Visible;
            _mainBoxLoadingControl.Visibility = Visibility.Collapsed;
        }

        private void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            FormData.TryRefresh();
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            FormData.SearchTerm = string.Empty;
        }

        private async void MenuItemNew_OnClick(object sender, RoutedEventArgs e)
        {
            AddTextKeyWindow addWindow = new AddTextKeyWindow(FormData.SelectedLanguage, new ObservableTranslationData(string.Empty, string.Empty, true), new ObservableTranslationData(string.Empty, string.Empty, true), new ObservableTranslationData(string.Empty, string.Empty, true), new ObservableTranslationData(string.Empty, string.Empty, true), new ObservableTranslationData(string.Empty, string.Empty, true)) { IsModal = true };
            if (!await this.ShowChildWindowAsync<bool>(addWindow, ChildWindowManager.OverlayFillBehavior.FullWindow))
            {
                return;
            }

            foreach (PGLanguage loadedLanguage in PGTEXTS.GetLoadedLanguages())
            {
                switch (loadedLanguage)
                {
                    case PGLanguage.ENGLISH:
                        if (string.IsNullOrEmpty(addWindow.FormData.EnglishText))
                        {
                            addWindow.FormData.EnglishText = $"TODO: {addWindow.FormData.FallbackText}";
                        }
                        PGTEXTS.SetText(addWindow.FormData.Key, addWindow.FormData.EnglishText, loadedLanguage);
                        if (FormData.Sources[loadedLanguage].Source is ObservableCollection<ObservableTranslationData> src_e)
                        {
                            FormData.IsEdited = true;
                            src_e.Add(addWindow.FormData.TranslationEnglish);
                        }
                        break;

                    case PGLanguage.FRENCH:
                        if (string.IsNullOrEmpty(addWindow.FormData.FrenchText))
                        {
                            addWindow.FormData.FrenchText = $"TODO: {addWindow.FormData.FallbackText}";
                        }
                        PGTEXTS.SetText(addWindow.FormData.Key, addWindow.FormData.FrenchText, loadedLanguage);
                        if (FormData.Sources[loadedLanguage].Source is ObservableCollection<ObservableTranslationData> src_f)
                        {
                            FormData.IsEdited = true;
                            src_f.Add(addWindow.FormData.TranslationFrench);
                        }
                        break;

                    case PGLanguage.ITALIAN:
                        if (string.IsNullOrEmpty(addWindow.FormData.ItalianText))
                        {
                            addWindow.FormData.ItalianText = $"TODO: {addWindow.FormData.FallbackText}";
                        }
                        PGTEXTS.SetText(addWindow.FormData.Key, addWindow.FormData.ItalianText, loadedLanguage);
                        if (FormData.Sources[loadedLanguage].Source is ObservableCollection<ObservableTranslationData> src_i)
                        {
                            FormData.IsEdited = true;
                            src_i.Add(addWindow.FormData.TranslationItalian);
                        }
                        break;

                    case PGLanguage.GERMAN:
                        if (string.IsNullOrEmpty(addWindow.FormData.GermanText))
                        {
                            addWindow.FormData.GermanText = $"TODO: {addWindow.FormData.FallbackText}";
                        }
                        PGTEXTS.SetText(addWindow.FormData.Key, addWindow.FormData.GermanText, loadedLanguage);
                        if (FormData.Sources[loadedLanguage].Source is ObservableCollection<ObservableTranslationData> src_g)
                        {
                            FormData.IsEdited = true;
                            src_g.Add(addWindow.FormData.TranslationGerman);
                        }
                        break;

                    case PGLanguage.SPANISH:
                        if (string.IsNullOrEmpty(addWindow.FormData.SpanishText))
                        {
                            addWindow.FormData.SpanishText = $"TODO: {addWindow.FormData.FallbackText}";
                        }
                        PGTEXTS.SetText(addWindow.FormData.Key, addWindow.FormData.SpanishText, loadedLanguage);
                        if (FormData.Sources[loadedLanguage].Source is ObservableCollection<ObservableTranslationData> src_s)
                        {
                            FormData.IsEdited = true;
                            src_s.Add(addWindow.FormData.TranslationSpanish);
                        }
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private void OnSearchClick(object sender, RoutedEventArgs e)
        {
            FormData.TryRefresh();
        }

        private async void MenuItemEdit_OnClick(object sender, RoutedEventArgs e)
        {
            DataGridRow row = UiUtility.TryFindParent<DataGridRow>(CurrentRightClickedObject);
            if (row == null)
            {
                CurrentRightClickedObject = null;
                return;
            }

            if (!(row.Item is ObservableTranslationData translationItem))
            {
                CurrentRightClickedObject = null;
                e.Handled = true;
                return;
            }
            ObservableTranslationData english = null;
            ObservableTranslationData german = null;
            ObservableTranslationData french = null;
            ObservableTranslationData italian = null;
            ObservableTranslationData spanish = null;
            PrepareEditWindow(translationItem, ref english, ref french, ref italian, ref german, ref spanish);
            EditTextKeyWindow w = new EditTextKeyWindow(FormData.SelectedLanguage, english, german, french, italian, spanish) { IsModal = true };
            await this.ShowChildWindowAsync<bool>(w, ChildWindowManager.OverlayFillBehavior.FullWindow);
            if (w.FormData.TranslationChanged)
            {
                FormData.IsEdited = true;
            }
            CurrentRightClickedObject = null;
            e.Handled = true;
        }

        private void PrepareEditWindow(ObservableTranslationData observableTranslationData, ref ObservableTranslationData english, ref ObservableTranslationData french, ref ObservableTranslationData italian, ref ObservableTranslationData german, ref ObservableTranslationData spanish)
        {
            foreach (PGLanguage loadedLanguage in PGTEXTS.GetLoadedLanguages())
            {
                switch (loadedLanguage)
                {
                    case PGLanguage.ENGLISH:
                        if (FormData.Sources[loadedLanguage].Source is ObservableCollection<ObservableTranslationData> en)
                        {
                            english = en.FirstOrDefault(p => p.Key == observableTranslationData.Key);
                            if (english == null)
                            {
                                english = new ObservableTranslationData(observableTranslationData.Key, string.Empty, true);
                                en.Add(english);
                            }
                        }

                        break;

                    case PGLanguage.FRENCH:
                        if (FormData.Sources[loadedLanguage].Source is ObservableCollection<ObservableTranslationData> fr)
                        {
                            french = fr.FirstOrDefault(p => p.Key == observableTranslationData.Key);
                            if (french == null)
                            {
                                french = new ObservableTranslationData(observableTranslationData.Key, string.Empty, true);
                                fr.Add(french);
                            }
                        }

                        break;

                    case PGLanguage.ITALIAN:
                        if (FormData.Sources[loadedLanguage].Source is ObservableCollection<ObservableTranslationData> it)
                        {
                            italian = it.FirstOrDefault(p => p.Key == observableTranslationData.Key);
                            if (italian == null)
                            {
                                italian = new ObservableTranslationData(observableTranslationData.Key, string.Empty, true);
                                it.Add(italian);
                            }
                        }

                        break;

                    case PGLanguage.GERMAN:
                        if (FormData.Sources[loadedLanguage].Source is ObservableCollection<ObservableTranslationData> ger)
                        {
                            german = ger.FirstOrDefault(p => p.Key == observableTranslationData.Key);
                            if (german == null)
                            {
                                german = new ObservableTranslationData(observableTranslationData.Key, string.Empty, true);
                                ger.Add(german);
                            }
                        }

                        break;

                    case PGLanguage.SPANISH:
                        if (FormData.Sources[loadedLanguage].Source is ObservableCollection<ObservableTranslationData> sp)
                        {
                            spanish = sp.FirstOrDefault(p => p.Key == observableTranslationData.Key);
                            if (spanish == null)
                            {
                                spanish = new ObservableTranslationData(observableTranslationData.Key, string.Empty, true);
                                sp.Add(spanish);
                            }
                        }

                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private void _basicEditorDataGrid_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            CurrentRightClickedObject = (DependencyObject)e.OriginalSource;
        }

        private async void MainWindow_OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bool shutdown = true;
            if (FormData.IsEdited)
            {
                e.Cancel = true;
                MetroDialogSettings mySettings = new MetroDialogSettings() { AffirmativeButtonText = "Quit", NegativeButtonText = "Cancel", AnimateShow = true, AnimateHide = false };

                MessageDialogResult result = await this.ShowMessageAsync("Quit application?", "The current data contains unsaved changes!\n" + "Closing the application will result in the loss of all changes.\n" + "If you want to keep your changes, export the data before continuing.\n\n" + "Continue without exporting?", MessageDialogStyle.AffirmativeAndNegative, mySettings);

                shutdown = result == MessageDialogResult.Affirmative;
            }

            if (shutdown)
            {
                Application.Current.Shutdown();
            }
        }

        private async void _verifyIntegrityButton_Click(object sender, RoutedEventArgs e)
        {
            MessageDialogResult dialogResult = await this.ShowMessageAsync("Warning!", "Performing a data set fixup will require you to save the translation data to a location of your choice in order to ensure you do not lose changes made by the fixup process.", MessageDialogStyle.AffirmativeAndNegative);
            if (dialogResult != MessageDialogResult.Affirmative)
            {
                return;
            }
            try
            {
                PGTEXTS.PerformTranslationFixup(Properties.Settings.Default.USR_MASTER_LANGUAGE);
                ImportFromPgTextSource();
                FormData.TryRefresh();
                await ExportToFile();
            }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Warning!", $"Something went wrong.\n{ex}");
            }
        }
    }
}
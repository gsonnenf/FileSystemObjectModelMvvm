using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Gstc.ViewModel;


namespace CoreVentingDesignCalculator {
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class TreeviewHeaderWithIcon : UserControl {
        #region properties


        public string IconName {
            set {
                try {
                    IconBitmapDecoder iconLoader = new IconBitmapDecoder(new Uri(value, UriKind.Relative), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    icon.Source = iconLoader.Frames[0];
                } catch (Exception e) {
                    throw e;
                    //ExceptionMessageBox.CreateExceptionMessageBox(e, "Icon could not be loaded.");

                }
            }
        }

        private string _oldText = "default";
        public string Text {
            set {
                _oldText = EditableTextBlock.Text;
                EditableTextBlock.Text = value;
            }
        }


        public Action OnDoubleClick;

        public Action OnRightClick;

        public Action OnLeftClick;

        public Func<string, bool> ValidateRename { get; set; }

        public string ToolTipMessage;


        public TreeviewHeaderWithIcon() {
            InitializeComponent();

        }

        #endregion

        public void RevertText() {
            EditableTextBlock.Text = _oldText;
        }

        private void UserControl_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
            //if (OnDoubleClick != null) OnDoubleClick();
            //textBlock.IsInEditMode = true;
        }

        private void UserControl_MouseRightButtonUp(object sender, MouseButtonEventArgs e) {
            OnRightClick?.Invoke();
        }

        private void UserControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            OnLeftClick?.Invoke();
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2) OnDoubleClick?.Invoke();
        }
    }

    public class FileSystemTypeToIconConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            FileSystemType type = ((FileSystemType)value);
            switch (type) {
                case FileSystemType.File: return "./icon/file.ico";
                case FileSystemType.Directory: return "./folder.ico";
                case FileSystemType.ObjectFile: return "./advanced.ico";
                default: return "./icons/monitor.ico";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}

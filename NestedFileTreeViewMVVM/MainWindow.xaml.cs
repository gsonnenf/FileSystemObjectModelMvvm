using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Gstc.ViewModel;


namespace NestedFileTreeViewMVVM {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private FileSystemViewModel TopViewModel;
        public MainWindow() {
            AdviceDispatcher.Dispatcher = Dispatcher;
            InitializeComponent();
            TopViewModel = FileSystemViewModel.CreateTopLevelViewModel("./level1/");

            DataContext = TopViewModel;

            TopViewModel.RecursiveOperation((viewModel2) => {
                viewModel2.Collection.CollectionChanged += (sender, args) =>  {
                    Console.WriteLine(viewModel2.DisplayName);
                };
            });


            TopViewModel.RecursiveOperation((viewModel2) => {
                viewModel2.PropertyChanged += delegate(object sender, PropertyChangedEventArgs args) {
                    Console.WriteLine(viewModel2.DisplayName);
                };
            });

        }

        private void MenuItem_Click(object sender, RoutedEventArgs e) {
            EditableText block = ((sender as MenuItem)?.Parent as ContextMenu)?.PlacementTarget as EditableText;
            Debug.Assert(block != null, "block != null");
            block.EditableTextBlock.IsEditing = true;
        }

    }
}

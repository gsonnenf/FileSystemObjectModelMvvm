using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PropertyChanged;

namespace Gstc.ViewModel {
    //[ImplementPropertyChanged] Added by fody already because of Interface.
    public class FileSystemViewModel : INotifyPropertyChanged, IDisposable {
        #region Static
        public static ObservableCollection<FileSystemViewModel> StaticCollection =
            new ObservableCollection<FileSystemViewModel>();

        static FileSystemViewModel() {
            StaticCollection.CollectionChanged += (obj, args) => { throw new InvalidOperationException( "The base file system view model of file type is not allowed to have members in its collection."); };
        }

        public static FileSystemViewModel CreateTopLevelViewModel(string fullPath) {
            if (!File.GetAttributes(fullPath).HasFlag(FileAttributes.Directory)) throw new Exception("Root directory must be a directory");
            return new FileSystemViewModel(fullPath);
        }
        #endregion

         
        internal MonitorFileSystem _monitorFileSystem;
        internal FileSystemViewModel _parent;

        #region Constructors and Destructor
       
        //For creating unattached object models
        public FileSystemViewModel(string displayName, FileSystemType fileSystemType) {
            var containsABadCharacter = new Regex("[" + Regex.Escape(Path.GetInvalidFileNameChars().ToString()) + "]");
            if (containsABadCharacter.IsMatch(displayName)) throw new ArgumentException("Display name is not valid.");
            DisplayName = displayName;
            Collection = (fileSystemType == FileSystemType.Directory)
                ? new ObservableCollection<FileSystemViewModel>()
                : StaticCollection;

            if (fileSystemType == FileSystemType.Directory) _monitorFileSystem = new MonitorDirectory(this);
            else if (fileSystemType == FileSystemType.File) _monitorFileSystem = new MonitorFile(this);
        }

        //Constructs initial file system object model of the directory
        private FileSystemViewModel(string fullPath) {
            var monitorDirectory = new MonitorDirectory(fullPath, this);
            InitializeConstructor(monitorDirectory, null);
        }

        //For creating file system models of already existing folders and files.
        private FileSystemViewModel(MonitorFileSystem monitor, FileSystemViewModel parent) {
            InitializeConstructor(monitor, parent);
        }

        // The standard initialization called in the constructors.
        private void InitializeConstructor(MonitorFileSystem monitor, FileSystemViewModel parent) {
            _monitorFileSystem = monitor;

            Collection = (_monitorFileSystem.FileSystemType == FileSystemType.Directory)
                ? new ObservableCollection<FileSystemViewModel>()
                : StaticCollection;

            _monitorFileSystem.ParentViewModel = this;
            _parent = parent;
            DisplayName = _monitorFileSystem.Name;
            DisplayNameChanged += OnDisplayNameChangedEvent;

            foreach (var child in _monitorFileSystem.GetChildren() ) Collection.Add(new FileSystemViewModel(child, this));
            (_monitorFileSystem as MonitorDirectory)?.InitializeFileWatcher();
            Collection.CollectionChanged += CollectionOnCollectionChanged;
        }

        public void Dispose() {
            _monitorFileSystem?.Dispose();
            foreach (var viewModel in Collection) viewModel.Dispose();
        }

        ~FileSystemViewModel() {
            Dispose();
        }
        #endregion

        #region public properties
        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<FileSystemViewModel> Collection { get; private set; }

        [DoNotNotify]
        public string FullPath => _monitorFileSystem.FullPath;

        public FileSystemType FileSystemType => _monitorFileSystem.FileSystemType;

        public string DisplayName { get; set; }

        #endregion
        /**************************************************************/


        #region FileSystemObjectModel Callbacks

        //Special method used to bind OnDisplayNameChanged to its event handler using Fody.
        private void OnDisplayNameChanged() { DisplayNameChanged?.Invoke(this, DisplayName); }
        internal EventHandler<string> DisplayNameChanged;

        internal void CollectionOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args) {
            switch (args.Action) {
                case NotifyCollectionChangedAction.Add: CollectionAdded(args); break;
                case NotifyCollectionChangedAction.Remove: CollectionRemoved(args); break;
                case NotifyCollectionChangedAction.Replace: break;
                case NotifyCollectionChangedAction.Move: break;
                case NotifyCollectionChangedAction.Reset:break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        internal void OnDisplayNameChangedEvent(object sender, string args) {
            (_parent?._monitorFileSystem as MonitorDirectory)?.ExecuteWithoutFileWatcherEvent( () => _monitorFileSystem.Rename(DisplayName));
        }

        private void CollectionAdded(NotifyCollectionChangedEventArgs args) {
            foreach (var item in args.NewItems) {
                var viewModel = item as FileSystemViewModel;
                if (viewModel == null) continue;
                Debug.Assert(_monitorFileSystem is MonitorDirectory);
                ((MonitorDirectory) _monitorFileSystem).ExecuteWithoutFileWatcherEvent( () => viewModel.CreateInFileSystem(this)); }
        }

        private void CollectionRemoved(NotifyCollectionChangedEventArgs args) {
            foreach (var item in args.OldItems) {
                var viewModel = item as FileSystemViewModel;
                if (viewModel == null) continue;
                Debug.Assert(_monitorFileSystem is MonitorDirectory);
                ((MonitorDirectory) _monitorFileSystem).ExecuteWithoutFileWatcherEvent( () => viewModel.DeleteFromFileSystem()); }
        }
        #endregion

        #region Find and recursion Utilities
        public FileSystemViewModel FindByPath(string fullPath, bool recursive = false) {
            if (recursive) throw new NotImplementedException(); //TODO: Make recursive.
            try {
                return (fullPath != null) ? Collection.First((model) => {
                    int compare = string.Compare( 
                        model.FullPath.TrimEnd( Path.DirectorySeparatorChar ), 
                        fullPath.TrimEnd( Path.DirectorySeparatorChar ), 
                        StringComparison.InvariantCultureIgnoreCase);
                    return (compare == 0);
                    //return model.FullPath == fullPath;
                }) : null;
            } catch (InvalidOperationException) { return null; }
        }

        public FileSystemViewModel FindByName(string name, bool recursive = false) {
            if (recursive) throw new NotImplementedException(); //TODO: Make recursive.
            try { return name != null ? Collection.First(model => model.DisplayName == name) : null; }
            catch (InvalidOperationException) { return null; }
        }

        public void RecursiveOperation(Action<FileSystemViewModel> operation) {
            operation(this);
            foreach (var viewModel in Collection) viewModel.RecursiveOperation(operation);
        }
        #endregion 
        
        #region File Creation Events

        protected virtual void CreateInFileSystem(FileSystemViewModel parent) {
            _parent = parent;
            _monitorFileSystem.ParentViewModel = this;
            _monitorFileSystem.Create(Path.Combine(parent.FullPath, DisplayName));
            DisplayNameChanged += OnDisplayNameChangedEvent;
            if (_monitorFileSystem.FileSystemType != FileSystemType.Directory) return;
            foreach (var viewModel in Collection) viewModel.CreateInFileSystem(this);
            Collection.CollectionChanged += CollectionOnCollectionChanged;
        }

        protected void DeleteFromFileSystem() {                     
            if (_monitorFileSystem.FileSystemType == FileSystemType.Directory) {
                Collection.CollectionChanged -= CollectionOnCollectionChanged;
                foreach (var viewModel in Collection) viewModel.DeleteFromFileSystem();
                Collection.Clear();
            }
            _monitorFileSystem.Delete();
            _monitorFileSystem = null;
        }

        internal FileSystemViewModel AddChildToObjectModel(string fullPath) {
            MonitorFileSystem monitor;
            if (File.GetAttributes(fullPath).HasFlag(FileAttributes.Directory)) monitor = new MonitorDirectory(fullPath, this);
            else monitor = new MonitorFile(fullPath, this);
            return new FileSystemViewModel(monitor, this);
        }

        #endregion
    }
}
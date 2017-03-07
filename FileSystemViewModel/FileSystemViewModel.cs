using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using ArxOne.MrAdvice.Advice;
using PropertyChanged;

namespace FileSystemViewModel {
    [ImplementPropertyChanged]
    public class FileSystemViewModel : IDisposable {
        
        protected readonly ObservableCollection<FileSystemViewModel> _collection;
        protected readonly bool _isDirectory;
        protected DirectoryInfo _directoryInfo;
        protected FileInfo _fileInfo;

        protected FileSystemWatcher _fileSystemWatcher;
        protected FileSystemViewModel _parent;
        //private FileSystemInfo _fileSystemInfo => (FileSystemInfo) _directoryInfo ?? _fileInfo;

        public FileSystemViewModel(string displayName, bool isDirectory) {
            var containsABadCharacter = new Regex("[" + Regex.Escape(Path.GetInvalidFileNameChars().ToString()) + "]");
            if (containsABadCharacter.IsMatch(displayName)) throw new ArgumentException("Display name is not valid.");
            DisplayName = displayName;
            _isDirectory = isDirectory;
            if (_isDirectory) _collection = new ObservableCollection<FileSystemViewModel>();
        }

        //Constructs initial file system object model of the directory
        private FileSystemViewModel(string fullPath, bool isDirectory, FileSystemViewModel parent) {
            _parent = parent;
            _isDirectory = isDirectory;
            if (!_isDirectory) {
                _fileInfo = new FileInfo(fullPath);
                DisplayName = _fileInfo.Name;
                return;
            }

            _directoryInfo = new DirectoryInfo(fullPath);
            DisplayName = _directoryInfo.Name;

            _collection = new ObservableCollection<FileSystemViewModel>();
            foreach (var fileInfo in _directoryInfo.GetFiles()) Collection.Add(new FileSystemViewModel(fileInfo.FullName, false, this));
            foreach (var dirInfo in _directoryInfo.GetDirectories()) Collection.Add(new FileSystemViewModel(dirInfo.FullName, true, this));

            InitializeFileWatcher();
            PropertyChanged += OnPropertyChangedFileRename;
            Collection.CollectionChanged += CollectionOnCollectionChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<FileSystemViewModel> Collection {
            get {
                if (!_isDirectory) throw new Exception( "File type ViewModel attempted to reference Collection. Only directory type viewModels can access Collection.");
                return _collection;
            }
        }

        public string FullPath => _parent == null ? _directoryInfo?.FullName : Path.Combine(_parent.FullPath, DisplayName);

        public string DisplayName { get; set; }

        protected void InitializeFileWatcher() {
            _fileSystemWatcher = new FileSystemWatcher();
            
            _fileSystemWatcher.Path = _directoryInfo.FullName;
            _fileSystemWatcher.IncludeSubdirectories = false;
            _fileSystemWatcher.Created += FileSystemWatcherOnCreated;
            _fileSystemWatcher.Deleted += FileSystemWatcherOnDeleted;
            _fileSystemWatcher.Renamed += FileSystemWatcherOnRenamed;
            _fileSystemWatcher.Changed += FileSystemWatcherOnChanged;
                     
            _fileSystemWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite| NotifyFilters.FileName | NotifyFilters.DirectoryName;
            _fileSystemWatcher.EnableRaisingEvents = true;
        }

        

        /**************************************************************/

        public static FileSystemViewModel CreateTopLevelViewModel(string fullPath) {
            if (!File.GetAttributes(fullPath).HasFlag(FileAttributes.Directory)) throw new Exception("Root directory must be a directory");
            return new FileSystemViewModel(fullPath, true, null);
        }

        #region Collection Synchronize Event Handlers

       
        protected void CollectionOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args) {
            switch (args.Action) {
                case NotifyCollectionChangedAction.Add: CollectionAdd(args); break;
                case NotifyCollectionChangedAction.Remove: CollectionRemove(args); break;
                case NotifyCollectionChangedAction.Replace: break;
                case NotifyCollectionChangedAction.Move: break;
                case NotifyCollectionChangedAction.Reset: break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        protected void OnPropertyChangedFileRename(object sender, PropertyChangedEventArgs propertyChangedEventArgs) {
            if (propertyChangedEventArgs.PropertyName == "DisplayName") {
                //Todo: fix source in same as destiniation filename
                _directoryInfo?.MoveTo(Path.Combine(_directoryInfo.Parent.FullName, _directoryInfo.Name));
                _fileInfo?.MoveTo(Path.Combine(_fileInfo.DirectoryName, _fileInfo.Name));
            }
        }

        [ThreadWpf]
        protected void FileSystemWatcherOnCreated(object sender, FileSystemEventArgs args) {
            var viewModel = Find(args.FullPath);
            if (viewModel != null) throw new FileLoadException("File or Directory Already Exits");
            Collection.CollectionChanged -= CollectionOnCollectionChanged;
            Collection.Add(new FileSystemViewModel(args.FullPath, File.GetAttributes(args.FullPath).HasFlag(FileAttributes.Directory), this));
            Collection.CollectionChanged += CollectionOnCollectionChanged;
        }

        [ThreadWpf]
        protected void FileSystemWatcherOnDeleted(object sender, FileSystemEventArgs args) {
            var viewModel = Find(args.FullPath);
            if (viewModel == null) throw new FileNotFoundException("File or directory was not found in collection.");
            Collection.CollectionChanged -= CollectionOnCollectionChanged;
            Collection.Remove(viewModel);
            Collection.CollectionChanged += CollectionOnCollectionChanged;
        }

        [ThreadWpf]
        protected void FileSystemWatcherOnRenamed(object sender, RenamedEventArgs args) {
            var viewModel = Find(args.OldFullPath);
            PropertyChanged -= OnPropertyChangedFileRename;
            viewModel.DisplayName = args.Name;
            PropertyChanged += OnPropertyChangedFileRename;
        }

        [ThreadWpf]
        protected void FileSystemWatcherOnChanged(object sender, FileSystemEventArgs fileSystemEventArgs) {
            Console.WriteLine("Changed Event: " + fileSystemEventArgs.FullPath);
        }

        #endregion

        #region Collection Event Handlers

        private void CollectionAdd(NotifyCollectionChangedEventArgs args) {
            foreach (var item in args.NewItems) {
                var viewModel = item as FileSystemViewModel;
                if (viewModel == null) continue;

                _fileSystemWatcher.EnableRaisingEvents = false;
                viewModel._parent = this;
                viewModel.Create();
                _fileSystemWatcher.EnableRaisingEvents = true;
            }
        }

        private void CollectionRemove(NotifyCollectionChangedEventArgs args) {
            foreach (var item in args.OldItems) {
                var viewModel = item as FileSystemViewModel;
                if (viewModel == null) continue;
                _fileSystemWatcher.EnableRaisingEvents = false;
                viewModel.Delete();
                _fileSystemWatcher.EnableRaisingEvents = true;
            }
        }

        #endregion

        #region File Creation Events
        protected virtual void Create() {
            if (_isDirectory) {
                _directoryInfo = Directory.CreateDirectory(FullPath);
                
                foreach (var viewModel in Collection) viewModel.Create();
                InitializeFileWatcher();
                PropertyChanged += OnPropertyChangedFileRename;
                Collection.CollectionChanged += CollectionOnCollectionChanged;
            }
            else {
                FileStream fileStream = File.Create(FullPath);
                fileStream.Close();
                fileStream.Dispose();
                _fileInfo = new FileInfo(FullPath);
                PropertyChanged += OnPropertyChangedFileRename;
            }
        }

        protected void Delete() {
            if (_isDirectory) {
                _fileSystemWatcher?.Dispose();
                _directoryInfo?.Delete(true);
                Collection.CollectionChanged -= CollectionOnCollectionChanged;
                foreach (var viewModel in Collection) viewModel.Delete();
                Collection.Clear();
            }
            _fileInfo?.Delete();
            _fileInfo = null;
            _directoryInfo = null;
            _fileSystemWatcher = null;
        }

        #endregion

        public FileSystemViewModel Find(string fullPath) {
            try { return (fullPath != null) ? Collection.First(model => model.FullPath == fullPath) : null; } 
            catch (InvalidOperationException) { return null; }
        }

        public void RecursiveOperation(Action<FileSystemViewModel> operation) {
            operation(this);
            if (_isDirectory) foreach (var viewModel in Collection) viewModel.RecursiveOperation(operation);
        }

        ~FileSystemViewModel() {
            _fileSystemWatcher?.Dispose();
        }

        public void Dispose() {
            _fileSystemWatcher?.Dispose();
            if (_isDirectory) foreach (var viewModel in Collection) viewModel.Dispose();     
        }
    }

    public class ThreadWpf : Attribute, IMethodAdvice {
        public static Dispatcher Dispatcher { get; set; }
        public void Advise(MethodAdviceContext context) {          
                if (Dispatcher != null) Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)( () => {context.Proceed();} ));
                else context.Proceed();         
        }
    }
   
}
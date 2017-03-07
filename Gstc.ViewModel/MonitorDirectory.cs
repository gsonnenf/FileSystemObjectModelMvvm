using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FileSystemModelView.Test")]

namespace Gstc.ViewModel {
    public class MonitorDirectory : MonitorFileSystem {
        internal DirectoryInfo _directoryInfo;
        internal FileSystemWatcher _fileSystemWatcher;

        #region Constructors
        public MonitorDirectory(FileSystemViewModel viewModel) {
            ParentViewModel = viewModel;
        }

        //Internal mechanism for returning 
        private MonitorDirectory(DirectoryInfo directoryInfo, FileSystemViewModel viewModel = null) {
            ParentViewModel = viewModel;
            _directoryInfo = directoryInfo;
            //InitializeFileWatcher(); //TODO: Can I initialize this here?
        }

        public MonitorDirectory(string fullPath, FileSystemViewModel viewModel) {
            ParentViewModel = viewModel;
            _directoryInfo = new DirectoryInfo(fullPath);
            InitializeFileWatcher();
        }

        public override void Dispose() {
            _fileSystemWatcher?.Dispose();
        }

        ~MonitorDirectory() {
            Dispose();
        }

        #endregion

        #region Overrides

        public override string Name => _directoryInfo.Name;

        public override string FullPath => _directoryInfo.FullName;

        public override FileSystemType FileSystemType => FileSystemType.Directory;

        public override string ParentPath => _directoryInfo?.Parent?.FullName;

        public override Collection<MonitorFileSystem> GetChildren() {
            var collection = new Collection<MonitorFileSystem>();
            foreach (var dirInfo in _directoryInfo.GetDirectories()) collection.Add(new MonitorDirectory(dirInfo));
            foreach (var fileInfo in _directoryInfo.GetFiles()) collection.Add(new MonitorFile(fileInfo));
            return collection;
        }
      
        public override void Create(string fullPath) {
            if (_directoryInfo != null)
                throw new FileFormatException("Attempted to create a file or directory that already exists.");
            _directoryInfo = Directory.CreateDirectory(fullPath);
            InitializeFileWatcher();
        }

        public override void Rename(string name) {
            if (_directoryInfo?.Name == name) return; //TODO: Solve problem with duplicate names better.
            _directoryInfo?.MoveTo(Path.Combine(_directoryInfo.Parent.FullName, name));
        }

        public override void UpdatePath(string fullPath) {
            _directoryInfo = new DirectoryInfo(fullPath);
        }

        public void ExecuteWithoutFileWatcherEvent(Action action) {
            //TODO: How to account for race conditions?
            //using (var file = new FileStream(FullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { 
            _fileSystemWatcher.EnableRaisingEvents = false;
            action();
            _fileSystemWatcher.EnableRaisingEvents = true;
        }

        public override void Delete() {
            _fileSystemWatcher.EnableRaisingEvents = false;
            _directoryInfo?.Delete(true);
            Dispose();
        }
        #endregion

        #region FileWatcherEvents

        public EventHandler<FileSystemEventArgs> Created;
        public EventHandler<FileSystemEventArgs> Deleted;
        public EventHandler<RenamedEventArgs> Renamed;
        public EventHandler<FileSystemEventArgs> Changed;

        [AdviceDispatcher]
        private void OnCreated(object sender, FileSystemEventArgs fileSystemEventArgs) {
            Created?.Invoke(this, fileSystemEventArgs);
        }

        [AdviceDispatcher]
        private void OnDeleted(object sender, FileSystemEventArgs fileSystemEventArgs) {
            Deleted?.Invoke(this, fileSystemEventArgs);
        }

        [AdviceDispatcher]
        private void OnRenamed(object sender, RenamedEventArgs fileSystemEventArgs) {
            Renamed?.Invoke(this, fileSystemEventArgs);
        }

        [AdviceDispatcher]
        private void OnChanged(object sender, FileSystemEventArgs fileSystemEventArgs) {
            Changed?.Invoke(this, fileSystemEventArgs);
        }

        private void OnCreatedFileSystem(object sender, FileSystemEventArgs args) {
            Console.WriteLine("First event");
            var viewModel = ParentViewModel.FindByPath(args.FullPath);
            if (viewModel != null) throw new FileLoadException("File or Directory Already Exits");
            lock (ParentViewModel.Collection) {
                ParentViewModel.Collection.CollectionChanged -= ParentViewModel.CollectionOnCollectionChanged;
                ParentViewModel.Collection.Add(ParentViewModel.AddChildToObjectModel(args.FullPath));
                ParentViewModel.Collection.CollectionChanged += ParentViewModel.CollectionOnCollectionChanged;
            }
        }

        private void OnDeletedFileSystem(object sender, FileSystemEventArgs args) {
            var viewModel = ParentViewModel.FindByPath(args.FullPath);
            if (viewModel == null) throw new FileNotFoundException("File or directory was not found in collection.");

            lock (ParentViewModel.Collection) {
                ParentViewModel.Collection.CollectionChanged -= ParentViewModel.CollectionOnCollectionChanged;
                ParentViewModel.Collection.Remove(viewModel);
                ParentViewModel.Collection.CollectionChanged += ParentViewModel.CollectionOnCollectionChanged;
            }
        }

        private void OnRenamedFileSystem(object sender, RenamedEventArgs args) {
            var changedViewModel = ParentViewModel.FindByName(args.OldName);
            if (changedViewModel == null) throw new ArgumentNullException("View Model was not found.");
            lock (ParentViewModel.Collection) lock (changedViewModel) {
                    changedViewModel.DisplayNameChanged -= changedViewModel.OnDisplayNameChangedEvent;
                    changedViewModel.DisplayName = args.Name;
                    changedViewModel._monitorFileSystem.UpdatePath(args.FullPath);
                    changedViewModel.DisplayNameChanged += changedViewModel.OnDisplayNameChangedEvent;
                }
        }

        private void OnChangedFileSystem(object sender, FileSystemEventArgs fileSystemEventArgs) {
            Console.WriteLine("Changed Event: " + fileSystemEventArgs.FullPath);
        }

        public void InitializeFileWatcher() {
            if (_fileSystemWatcher != null) return;
            _fileSystemWatcher = new FileSystemWatcher();
            //_fileSystemWatcher.SynchronizingObject = new SynchronizeInvokeWpfWrapper(Dispatcher.CurrentDispatcher); 

            _fileSystemWatcher.Path = _directoryInfo.FullName;
            _fileSystemWatcher.IncludeSubdirectories = false;
            _fileSystemWatcher.Created += OnCreated;
            _fileSystemWatcher.Deleted += OnDeleted;
            _fileSystemWatcher.Renamed += OnRenamed;
            _fileSystemWatcher.Changed += OnChanged;

            Created += OnCreatedFileSystem;
            Deleted += OnDeletedFileSystem;
            Renamed += OnRenamedFileSystem;
            Changed += OnChangedFileSystem;

            _fileSystemWatcher.NotifyFilter = 
                NotifyFilters.LastAccess | NotifyFilters.LastWrite |                                            
                NotifyFilters.FileName | NotifyFilters.DirectoryName;

            _fileSystemWatcher.EnableRaisingEvents = true;
        }

        #endregion
    }
}
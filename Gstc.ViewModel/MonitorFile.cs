using System;
using System.Collections.ObjectModel;
using System.IO;

namespace Gstc.ViewModel {
    internal class MonitorFile : MonitorFileSystem {
        public static Collection<MonitorFileSystem> StaticCollection = new Collection<MonitorFileSystem>();
       
        private FileInfo _fileInfo;

        public MonitorFile(FileSystemViewModel viewModel) { ParentViewModel = viewModel; }

        public MonitorFile( FileInfo fileInfo, FileSystemViewModel viewModel = null) {
            ParentViewModel = viewModel;
            _fileInfo = fileInfo;
        }

        public MonitorFile( string fullPath, FileSystemViewModel viewModel = null) {
            ParentViewModel = viewModel;
            _fileInfo = new FileInfo(fullPath);
            if (_fileInfo == null) throw new NullReferenceException();
        }

        public override string Name => _fileInfo.Name;

        public override string FullPath => _fileInfo?.FullName;

        public override FileSystemType FileSystemType => FileSystemType.File;

        public override string ParentPath => _fileInfo?.DirectoryName;

        public override void Rename(string name) {
            if (_fileInfo?.Name == name) return; //TODO: Solve problem with duplicate names better.
            _fileInfo?.MoveTo(Path.Combine(_fileInfo.DirectoryName, name));
            //TODO: Check if we need to update _fileInfo
        }

        public override Collection<MonitorFileSystem> GetChildren() { return StaticCollection; }

        public override void Create(string fullPath) {
            using (File.Create(fullPath)) UpdatePath(fullPath);
        }

        public override void UpdatePath(string fullPath) { _fileInfo = new FileInfo(fullPath); }

        public override void Delete() {  _fileInfo?.Delete(); }

        public override void Dispose() {}
    }
}
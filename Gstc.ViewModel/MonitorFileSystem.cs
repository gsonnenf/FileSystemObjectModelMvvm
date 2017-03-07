using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gstc.ViewModel {
    public abstract class MonitorFileSystem : IDisposable {

        public abstract string Name { get; }
        public abstract string FullPath { get; }
        public abstract string ParentPath { get; }
        public abstract void Create(string fullPath);
        public abstract void Delete();
        public abstract void Rename(string name);
        public abstract void UpdatePath(string path);
        public abstract FileSystemType FileSystemType { get; }
        public FileSystemViewModel ParentViewModel { get; set; }


        public abstract Collection<MonitorFileSystem> GetChildren();
        public abstract void Dispose();
    }

    public enum FileSystemType {
        Directory,
        File,
        ObjectFile
    } 
}

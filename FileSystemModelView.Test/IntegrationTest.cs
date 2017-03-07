using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Gstc.ViewModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Directory = System.IO.Directory;

namespace FileSystemModelView.Test {
    [TestClass]
    public class IntegrationTest {
     

        [TestMethod]
        public void CollectionCreateTest() {
            AdviceDispatcher.Dispatcher = Dispatcher.CurrentDispatcher;
            //Creates test directories
            string baseTestFolderName = "./Level1/";
            if (Directory.Exists(baseTestFolderName)) Directory.Delete(baseTestFolderName, true);
            Assert.IsFalse(Directory.Exists(baseTestFolderName));
            Directory.CreateDirectory(baseTestFolderName);
            var subDir1 = Directory.CreateDirectory(Path.Combine(baseTestFolderName, "Level2a"));
            Directory.CreateDirectory(Path.Combine(baseTestFolderName, "Level2b"));
            Directory.CreateDirectory(Path.Combine(baseTestFolderName, "Level2c"));
            var subDir2 = Directory.CreateDirectory(Path.Combine(subDir1.FullName, "Level3aa"));

            //Creates test Files
            string filePath1;
            string filePath2;
            using (FileStream fileStream = File.Create(Path.Combine(baseTestFolderName, "file1.txt"))) filePath1 = fileStream.Name;
            using (FileStream fileStream = File.Create(Path.Combine(subDir2.FullName, "file3aa.txt"))) filePath2 = fileStream.Name;

            Thread.Sleep(100);

            //Creates the FileSystem object view model
            FileSystemViewModel topLevelViewModel = FileSystemViewModel.CreateTopLevelViewModel(baseTestFolderName);

            //Checks the objects of the files are created. 
            Assert.AreEqual(topLevelViewModel.Collection.Count, 4);
            Assert.IsNull(topLevelViewModel.FindByPath("./notFound/"));
            Assert.IsNotNull(topLevelViewModel.FindByPath(filePath1));
            Assert.IsNotNull(topLevelViewModel.FindByPath(subDir1.FullName));           
            Assert.IsNotNull(topLevelViewModel.FindByPath(subDir1.FullName).FindByPath(subDir2.FullName));
            Assert.IsNotNull(topLevelViewModel.FindByPath(subDir1.FullName).FindByPath(subDir2.FullName).FindByPath(filePath2));

            topLevelViewModel.RecursiveOperation( (viewModel) => {
                Console.WriteLine(viewModel.DisplayName);
                Console.WriteLine(viewModel.FullPath);
            });

            //Adds in new stuff
            string testFolderAddName1 = "testFolderAddName1";
            topLevelViewModel.Collection.Add(new FileSystemViewModel(testFolderAddName1, FileSystemType.Directory));
            Assert.IsTrue(Directory.Exists(Path.Combine(baseTestFolderName, testFolderAddName1)));

            string testFileAddName1 = "testFileAddName1.txt";
            topLevelViewModel.Collection.Add(new FileSystemViewModel(testFileAddName1, FileSystemType.File));
            Assert.IsTrue(File.Exists(Path.Combine(baseTestFolderName, testFileAddName1)));


            string testFolderAddName2 = "testFolderAddName2";
            topLevelViewModel.Collection[0].Collection.Add(new FileSystemViewModel(testFolderAddName2, FileSystemType.Directory));
            Assert.IsTrue(Directory.Exists(Path.Combine(topLevelViewModel.Collection[0].FullPath, testFolderAddName2)));

            string testFileAddName2 = "testFileAddName2.txt";
            topLevelViewModel.Collection[0].Collection[1].Collection.Add(new FileSystemViewModel(testFileAddName2, FileSystemType.File));
            Assert.IsTrue(File.Exists(Path.Combine(topLevelViewModel.Collection[0].Collection[1].FullPath, testFileAddName2)));

            Console.WriteLine("Added:");
            topLevelViewModel.RecursiveOperation((viewModel) => {
                Console.WriteLine(viewModel.DisplayName);
                Console.WriteLine(viewModel.FullPath);
            });

            topLevelViewModel.Dispose();
        }

        [TestMethod]
        public void CollectionRenamedTest() {
            AdviceDispatcher.Dispatcher = Dispatcher.CurrentDispatcher;
            //Ensures folder environment is working
            string baseTestFolderName = "./CollectionRenameTest/";
            if (Directory.Exists(baseTestFolderName)) Directory.Delete(baseTestFolderName, true);
            Assert.IsFalse(Directory.Exists(baseTestFolderName));
            Directory.CreateDirectory(baseTestFolderName);

            string dirName = "Dir1";
            var subDir1 = Directory.CreateDirectory(Path.Combine(baseTestFolderName, dirName));
            string fileName = "file1.txt";
            using (File.Create(Path.Combine(baseTestFolderName, fileName))) ;

            FileSystemViewModel fileSystemViewModel = FileSystemViewModel.CreateTopLevelViewModel(baseTestFolderName);

            var viewModel = fileSystemViewModel.FindByName(dirName);
            string changedDirName = "CollectionChangedDirectory";
            viewModel.DisplayName = changedDirName;
            Assert.IsFalse(Directory.Exists(Path.Combine(baseTestFolderName,dirName)));
            Assert.IsTrue(Directory.Exists(Path.Combine(baseTestFolderName, changedDirName)));

            var viewModel2 = fileSystemViewModel.FindByName(fileName);
            string changedFileName = "CollectionChangedFile.txt";
            viewModel2.DisplayName = changedFileName;
            Assert.IsFalse(File.Exists(Path.Combine(baseTestFolderName, fileName)));
            Assert.IsTrue(File.Exists(Path.Combine(baseTestFolderName, changedFileName)));

            fileSystemViewModel.Dispose();
        }

        

        [TestMethod]
        [Timeout(5000)]
        public void FileWatcherCreateAndRenameTest() {
            AdviceDispatcher.Dispatcher = Dispatcher.CurrentDispatcher;
 
            string baseTestFolderName = "./fileWatcherTest/";
            CleanupDirectory(baseTestFolderName);
            Directory.CreateDirectory(Path.Combine(baseTestFolderName));

            FileSystemViewModel fileSystemViewModel = FileSystemViewModel.CreateTopLevelViewModel(baseTestFolderName);
            MonitorDirectory monitor = ((MonitorDirectory)fileSystemViewModel._monitorFileSystem);

            //Test 1 directory created in filesystem
            string addDirectoryName = "testDirectory";
            string addDirPath = Path.Combine(baseTestFolderName, addDirectoryName);
            Assert.IsFalse(Directory.Exists(addDirPath));
            var signal = new AwaitSignal(ref monitor.Created);
            DirectoryInfo info = Directory.CreateDirectory(addDirPath);
            Assert.IsTrue(Directory.Exists(addDirPath));
            Console.WriteLine("Main Thread id: " + Thread.CurrentThread.ManagedThreadId);
            signal.Wait(ref monitor.Created);
            FileSystemViewModel view = fileSystemViewModel.FindByPath(info.FullName);
            Assert.IsTrue(Directory.Exists(addDirPath));
            Assert.IsNotNull(view);


            return;
            //Test 2 File created in filesystem
            string AddFileName = "testFileAddName.txt";
            string addFilePath = Path.Combine(baseTestFolderName, AddFileName);
            Assert.IsFalse(File.Exists(addFilePath));
            signal = new AwaitSignal(ref monitor.Created);
            using (File.Create(addFilePath)) {signal.Wait(ref monitor.Created);};
            FileSystemViewModel view2 = fileSystemViewModel.FindByName(AddFileName);
            Assert.IsTrue(File.Exists(addFilePath));
            Assert.IsNotNull(view2);


            //Test 3 Directory Rename
            string moveDirName = "ChangedDirectory";
            string moveDirPath = Path.Combine(baseTestFolderName, moveDirName);
            Directory.Move(addDirPath, moveDirPath);
            Assert.IsFalse(Directory.Exists(addDirPath));
            Assert.IsTrue(Directory.Exists(moveDirPath));
            Thread.Sleep(100);
            Assert.IsNull(fileSystemViewModel.FindByName(addDirectoryName));
            Assert.IsNotNull(fileSystemViewModel.FindByName(moveDirName));

            //Test 4 File Rename Test
            string moveFileName = "ChangedName.txt";
            string moveFilePath = Path.Combine(baseTestFolderName, moveFileName);
            File.Move(addFilePath, moveFilePath);
            Assert.IsFalse(File.Exists(addFilePath));
            Assert.IsTrue(File.Exists(moveFilePath));

            Thread.Sleep(100);

            Assert.IsNull(fileSystemViewModel.FindByName(AddFileName));
            Assert.IsNotNull(fileSystemViewModel.FindByName(moveFileName));


            //Clean up
            fileSystemViewModel.Dispose();

        }

       

        [ClassCleanup]
        public static void CleanupMethod1() {
            CleanupDirectory("./fileWatcherTest/");
            CleanupDirectory("./Level1/");
            CleanupDirectory("./CollectionRenameTest/");
        }

        private static void CleanupDirectory(string path) {
            try {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            } catch (Exception e) {
                Thread.Sleep(10);
                Console.WriteLine(e.Message);
                //Directory.Delete("./Level1/", true); //Todo: find out why this throws a access exception, filewatcher related?
            }
        }
    }

    public class AwaitSignal {

        static Action EmptyAction = () => { };

        static void DoEvents() {
            Dispatcher.CurrentDispatcher.Invoke(EmptyAction, DispatcherPriority.Normal);
        }

        private readonly AutoResetEvent _onCreateSignal;
        private readonly EventHandler<FileSystemEventArgs> _action;

        public AwaitSignal(ref EventHandler<FileSystemEventArgs> eventHandler) {
            _onCreateSignal = new AutoResetEvent(false);
            _action = (sender, e) => _onCreateSignal.Set();
            eventHandler += _action;
        }
  
        public void Wait(ref EventHandler<FileSystemEventArgs> eventHandler) {
            while (!_onCreateSignal.WaitOne(1)) DoEvents();
            eventHandler -= _action;
        }

    }
}

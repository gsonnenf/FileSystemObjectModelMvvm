using System;
using System.ComponentModel;
using System.Threading;
using System.Windows.Threading;

namespace Gstc.ViewModel {
    internal class SynchronizeInvokeWpfWrapper : ISynchronizeInvoke {
        private readonly Dispatcher m_disp;

        public SynchronizeInvokeWpfWrapper(Dispatcher dispatcher) {  m_disp = dispatcher; }

        #region IAsyncResult implementation

        private class DispatcherAsyncResultAdapter : IAsyncResult {

            public DispatcherAsyncResultAdapter(DispatcherOperation operation) { Operation = operation; }

            public DispatcherAsyncResultAdapter(DispatcherOperation operation, object state) : this(operation) { AsyncState = state; }

            public DispatcherOperation Operation { get; }

            public object AsyncState { get; }

            public WaitHandle AsyncWaitHandle => null;

            public bool CompletedSynchronously => false;

            public bool IsCompleted => Operation.Status == DispatcherOperationStatus.Completed;
        }

        #endregion

        #region ISynchronizeInvoke Members

        public IAsyncResult BeginInvoke(Delegate method, object[] args) {
            if (args != null && args.Length > 1) {
                var argsSansFirst = GetArgsAfterFirst(args);
                var op = m_disp.BeginInvoke(DispatcherPriority.Normal, method, args[0], argsSansFirst);
                return new DispatcherAsyncResultAdapter(op);
            }
            return args != null ? new DispatcherAsyncResultAdapter(m_disp.BeginInvoke(DispatcherPriority.Normal, method, args[0])) 
                : new DispatcherAsyncResultAdapter(m_disp.BeginInvoke(DispatcherPriority.Normal, method));
        }

        private static object[] GetArgsAfterFirst(object[] args) {
            var result = new object[args.Length - 1];
            Array.Copy(args, 1, result, 0, args.Length - 1);
            return result;
        }

        public object EndInvoke(IAsyncResult result) {
            var res = result as DispatcherAsyncResultAdapter;
            if (res == null) throw new InvalidCastException();

            while (res.Operation.Status != DispatcherOperationStatus.Completed ||
                   res.Operation.Status == DispatcherOperationStatus.Aborted) Thread.Sleep(50);

            return res.Operation.Result;
        }

        public object Invoke(Delegate method, object[] args) {
            if (args != null && args.Length > 1) {
                var argsSansFirst = GetArgsAfterFirst(args);
                return m_disp.Invoke(DispatcherPriority.Normal, method, args[0], argsSansFirst);
            }
            return args != null ? m_disp.Invoke(DispatcherPriority.Normal, method, args[0]) : m_disp.Invoke(DispatcherPriority.Normal, method);
        }

        public bool InvokeRequired => m_disp.Thread != Thread.CurrentThread;

        #endregion
    }
}
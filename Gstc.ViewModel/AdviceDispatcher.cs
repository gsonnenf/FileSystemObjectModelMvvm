using System;
using System.Threading;
using System.Windows.Threading;
using ArxOne.MrAdvice.Advice;

namespace Gstc.ViewModel {
    public class AdviceDispatcher : Attribute, IMethodAdvice {
        public static Dispatcher Dispatcher { get; set; }
        public void Advise(MethodAdviceContext context) {
            if (Dispatcher != null) Dispatch1( ()=> context.Proceed() );            
            else context.Proceed();
        }

        

        public static void Dispatch1(Action action) {
            Dispatcher.InvokeAsync(action, DispatcherPriority.Normal);           
            //Application.Current.Dispatcher.Invoke(onPropertyChangedAction);
            //Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { }));
            //Dispatcher.Thread.Interrupt();
        }

        public static void Dispatch2(MethodAdviceContext context) {
            DispatcherFrame frame = new DispatcherFrame();
            DispatcherOperationCallback callback = (obj)=> { context.Proceed(); return null; };
            Dispatcher.BeginInvoke(DispatcherPriority.Send, callback, frame);
            Dispatcher.PushFrame(frame);
        }

        

    }
}

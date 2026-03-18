using System;
using System.Collections.Generic;
using System.Text;

namespace VideoManager.ViewModels
{
    public class MainViewModel : BindableBase
    {
        public DelegateCommand<object> NavCommand { get; set; }

        private object pageContent;
        public object PageContent { set => SetProperty(ref pageContent, value); get => pageContent; }

        public MainViewModel()
        {
            NavCommand = new DelegateCommand<object>(OnNavCommand);
        }

        private void OnNavCommand(object obj)
        {
            PageContent = obj;
        }
    }
}

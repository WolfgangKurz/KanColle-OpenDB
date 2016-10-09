using System;
using System.Reactive.Linq;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grabacr07.KanColleViewer.Composition;

using KanColleOpenDB;
using KanColleOpenDB.Views;
using KanColleOpenDB.ViewModels;

namespace KanColleOpenDB
{
    [Export(typeof(IPlugin))]
    [Export(typeof(ITool))]
    [ExportMetadata("Guid", "B139EAC7-933F-4B35-9EE9-048B8F9F08E5")]
    [ExportMetadata("Title", "KanColleOpenDB")]
    [ExportMetadata("Description", "KanColleOpenDB for KanColleViewer")]
    [ExportMetadata("Version", "1.0.4")]
    [ExportMetadata("Author", "WolfgangKurz")]
    public class KanColleOpenDBPlugin : IPlugin, ITool
    {
        private OpenDBViewModel viewModel;
        string ITool.Name => "OpenDB Plugin";
        object ITool.View => new OpenDBView { DataContext = this.viewModel };

        public void Initialize()
        {
            this.viewModel = new OpenDBViewModel();
        }
    }
}

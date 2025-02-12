namespace SCaddins.ExportSchedules
{
    using System;
    using Autodesk.Revit.DB;
    using Autodesk.Revit.UI;

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
    [Autodesk.Revit.Attributes.Journaling(Autodesk.Revit.Attributes.JournalingMode.NoCommandData)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (commandData == null)
            {
                throw new ArgumentNullException(nameof(commandData));
            }

            Document doc = commandData.Application.ActiveUIDocument.Document;
            if (doc == null)
            {
                return Result.Failed;
            }

            var exportDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var vm = new ViewModels.ExportSchedulesViewModel(Utilities.GetAllSchedules(doc), exportDir);
            SCaddinsApp.WindowManager.ShowDialogAsync(vm, null, ViewModels.ExportSchedulesViewModel.DefaultWindowSettings);
            return Result.Succeeded;
        }
    }
}

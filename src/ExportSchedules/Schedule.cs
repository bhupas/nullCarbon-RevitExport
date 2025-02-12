namespace SCaddins.ExportSchedules
{
    using Autodesk.Revit.DB;
    using System.IO;

    public class Schedule
    {
        public Schedule(Autodesk.Revit.DB.ViewSchedule viewSchedule)
        {
            RevitViewSchedule = viewSchedule;
            RevitName = RevitViewSchedule.Name;
            ExportName = RevitName + ".txt";
        }

        public string ExportName { get; set; }
        public string RevitName { get; set; }

        public string Type
        {
            get
            {
                var scheduleDefinition = RevitViewSchedule.Definition;
                var scheduleCategory = scheduleDefinition.CategoryId;
                var categories = RevitViewSchedule.Document.Settings.Categories;
                if (!RevitViewSchedule.IsTitleblockRevisionSchedule)
                {
                    foreach (Category category in categories)
                    {
                        if (category.Id == scheduleCategory)
                        {
                            return category.Name;
                        }
                    }
                    return "<Multi-Category>";
                }
                return string.Empty;
            }
        }

        public Autodesk.Revit.DB.ViewSchedule RevitViewSchedule { get; set; }

        public override string ToString()
        {
            return RevitName;
        }

        public bool Export(ViewScheduleExportOptions options, string exportPath)
        {
            if (!Directory.Exists(exportPath))
            {
                return false;
            }
            try
            {
                // Build the full path for the CSV file.
                string filePath = Path.Combine(exportPath, ExportName);
                // If a file with the same name exists, delete it.
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                RevitViewSchedule.Export(exportPath, ExportName, options);
            }
            catch
            {
                return false;
            }
            return true;
        }
    }
}

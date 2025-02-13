namespace SCaddins.ExportSchedules
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Autodesk.Revit.DB;
    using OfficeOpenXml;

    public class Utilities
    {
        /// <summary>
        /// (Optional) Imports CSV data into an Excel workbook.
        /// This method is kept for reference if needed.
        /// </summary>
        public static void AddDelimitedDataToExcelWorkbook(string excelFileName, string worksheetName, string csvFileName)
        {
            bool firstRowIsHeader = false;
            var format = new ExcelTextFormat();
            // Use tab as the delimiter.
            format.Delimiter = '\t';
            format.EOL = "\r\n";
            // Use the default text qualifier (double quote).
            format.TextQualifier = '\"';

            using (ExcelPackage package = new ExcelPackage(new FileInfo(excelFileName)))
            {
                ExcelWorksheet worksheet = package.Workbook.Worksheets.Add(worksheetName);
                worksheet.Cells["A1"].LoadFromText(new FileInfo(csvFileName), format, OfficeOpenXml.Table.TableStyles.Medium27, firstRowIsHeader);
                package.Save();
            }
        }

        /// <summary>
        /// Exports the given schedules to a merged Excel workbook and saves individual CSV files for each schedule.
        /// For each schedule, a temporary CSV file is created by schedule.Export.
        /// Then each CSV is added as a worksheet in one Excel workbook.
        /// The CSV data is assumed to be delimited based on the user-selected options.
        /// Unlike before, the CSV files are no longer deleted so that both file formats are available.
        /// </summary>
        /// <param name="schedules">The list of schedules to export.</param>
        /// <param name="exportPath">The directory to export to.</param>
        /// <returns>A message summarizing the export results.</returns>
        public static string Export(List<Schedule> schedules, string exportPath)
        {
            StringBuilder exportMsg = new StringBuilder();
            int successes = 0;
            int attempts = 0;

            // Define the path for the merged Excel file.
            string mergedExcelFilePath = Path.Combine(exportPath, "MergedSchedules.xlsx");

            // Create a new ExcelPackage for the merged file.
            using (ExcelPackage mergedPackage = new ExcelPackage())
            {
                using (var options = LoadSavedExportOptions())
                {
                    foreach (var schedule in schedules)
                    {
                        attempts++;
                        if (!Directory.Exists(exportPath))
                        {
                            exportMsg.AppendLine($"[Error] {schedule.ExportName}. Directory not found: {exportPath}");
                            continue;
                        }
                        // Export the schedule to a temporary CSV file.
                        if (schedule.Export(options, exportPath))
                        {
                            string csvFilePath = Path.Combine(exportPath, schedule.ExportName);
                            try
                            {
                                // Remove the file extension from schedule.ExportName to use as the worksheet name.
                                string sheetName = Path.GetFileNameWithoutExtension(schedule.ExportName);
                                // Create a new worksheet in the merged Excel file.
                                ExcelWorksheet worksheet = mergedPackage.Workbook.Worksheets.Add(sheetName);

                                // Configure the text format for reading the CSV.
                                bool firstRowIsHeader = false;

                                // Use the user-selected field delimiter from options.
                                char delimiterChar = options.FieldDelimiter != null && options.FieldDelimiter.Length > 0
                                    ? options.FieldDelimiter[0]
                                    : '\t';

                                // Determine the text qualifier based on the user-selected option.
                                char textQualifierChar = '\"'; // default to double quote
                                switch (options.TextQualifier)
                                {
                                    case ExportTextQualifier.DoubleQuote:
                                        textQualifierChar = '\"';
                                        break;
                                    case ExportTextQualifier.Quote:
                                        textQualifierChar = '\'';
                                        break;
                                    case ExportTextQualifier.None:
                                        textQualifierChar = '\0'; // no text qualifier
                                        break;
                                    default:
                                        textQualifierChar = '\"';
                                        break;
                                }

                                var format = new ExcelTextFormat
                                {
                                    Delimiter = delimiterChar,
                                    EOL = "\r\n",
                                    TextQualifier = textQualifierChar,
                                    Culture = new CultureInfo("da-DK")
                                };

                                // Read the CSV file as text.
                                string csvContent = File.ReadAllText(csvFilePath);
                                // Load the CSV data into the worksheet using the string overload.
                                worksheet.Cells["A1"].LoadFromText(csvContent, format, OfficeOpenXml.Table.TableStyles.Medium27, firstRowIsHeader);

                                exportMsg.AppendLine($"[Success] {schedule.ExportName}");
                                successes++;
                            }
                            catch (Exception ex)
                            {
                                exportMsg.AppendLine($"[Error] Merging {schedule.ExportName}: {ex.Message}");
                            }
                            // NOTE: We are no longer deleting the temporary CSV file.
                            // This change ensures that both the merged Excel file and individual CSV files remain.
                        }
                        else
                        {
                            exportMsg.AppendLine($"[Error] {schedule.ExportName}");
                        }
                    }
                }
                // If the merged Excel file already exists, delete it to avoid crashing.
                if (File.Exists(mergedExcelFilePath))
                {
                    File.Delete(mergedExcelFilePath);
                }
                // Save the merged Excel workbook.
                mergedPackage.SaveAs(new FileInfo(mergedExcelFilePath));
            }

            int fails = attempts - successes;
            string summaryString = string.Format(
                "Export Summary:" + Environment.NewLine +
                "{0} Export(s) attempted with {1} success(es) and {2} fail(s)" + Environment.NewLine + Environment.NewLine,
                attempts,
                successes,
                fails);
            exportMsg.Insert(0, summaryString);
            exportMsg.AppendLine($"[Merged Excel] Merged Excel file created at {mergedExcelFilePath}");
            exportMsg.AppendLine($"[CSV Files] CSV files for each schedule are saved in {exportPath}");
            return exportMsg.ToString();
        }

        /// <summary>
        /// Retrieves all schedules from the given Revit document.
        /// </summary>
        public static List<Schedule> GetAllSchedules(Document doc)
        {
            List<Schedule> result = new List<Schedule>();
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(ViewSchedule));
            foreach (var elem in collector)
            {
                if (elem is ViewSchedule schedule)
                {
                    if (schedule.IsTitleblockRevisionSchedule || schedule.IsInternalKeynoteSchedule)
                        continue;
                    result.Add(new Schedule(schedule));
                }
            }
            return result;
        }

        /// <summary>
        /// Returns a dictionary of available field delimiters.
        /// </summary>
        public static Dictionary<string, string> GetFieldDelimiters()
        {
            Dictionary<string, string> result = new Dictionary<string, string>
            {
                { "Comma", "," },
                { "Semi-Colon", ";" },
                { "Tab", "\t" },
                { "Space", " " }
            };
            return result;
        }

        /// <summary>
        /// Returns a dictionary of available text qualifiers.
        /// </summary>
        public static Dictionary<string, string> GetFieldTextQualifiers()
        {
            Dictionary<string, string> result = new Dictionary<string, string>
            {
                { "Double Quote (\")", "\"" },
                { "Quote (\')", "\'" },
                { "None", string.Empty }
            };
            return result;
        }

        /// <summary>
        /// Loads saved export options from your application settings.
        /// </summary>
        private static ViewScheduleExportOptions LoadSavedExportOptions()
        {
            ViewScheduleExportOptions options = new ViewScheduleExportOptions();
            var headerExportType = Settings.Default.ExportColumnHeader ? ExportColumnHeaders.OneRow : ExportColumnHeaders.None;
            if (Settings.Default.IncludeGroupedColumnHeaders)
            {
                headerExportType = ExportColumnHeaders.MultipleRows;
            }
            options.ColumnHeaders = headerExportType;
            // Set default field delimiter to tab ("\t") if not provided.
            options.FieldDelimiter = Settings.Default.FieldDelimiter ?? "\t";
            options.HeadersFootersBlanks = Settings.Default.ExportGrouppHeaderAndFooters;
            string textQualifier = Settings.Default.TextQualifier;
            if (textQualifier == "\"")
            {
                options.TextQualifier = ExportTextQualifier.DoubleQuote;
            }
            else if (textQualifier == "\'")
            {
                options.TextQualifier = ExportTextQualifier.Quote;
            }
            else if (textQualifier == string.Empty)
            {
                options.TextQualifier = ExportTextQualifier.None;
            }
            options.Title = Settings.Default.ExportTitle;
            return options;
        }

        /// <summary>
        /// Opens a file selection dialog to choose an Excel file name.
        /// </summary>
        private static string SelectExcelFileName()
        {
            string filePath = string.Empty;
            SCaddinsApp.WindowManager.ShowFileSelectionDialog(null, out filePath);
            return filePath;
        }
    }
}

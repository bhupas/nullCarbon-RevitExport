using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using OfficeOpenXml;

namespace SCaddins.ExportSchedules
{
    public class Utilities
    {
        /// <summary>
        /// (Optional) Imports CSV data into an Excel workbook.
        /// This method is kept for reference if needed.
        /// </summary>
        public static void AddDelimitedDataToExcelWorkbook(string excelFileName, string worksheetName, string csvFileName)
        {
            bool firstRowIsHeader = false;
            var format = new ExcelTextFormat
            {
                // Use tab as the delimiter.
                Delimiter = '\t',
                EOL = "\r\n",
                // Default text qualifier (double quote).
                TextQualifier = '\"'
            };

            using (ExcelPackage package = new ExcelPackage(new FileInfo(excelFileName)))
            {
                ExcelWorksheet worksheet = package.Workbook.Worksheets.Add(worksheetName);
                worksheet.Cells["A1"].LoadFromText(
                    new FileInfo(csvFileName),
                    format,
                    OfficeOpenXml.Table.TableStyles.Medium27,
                    firstRowIsHeader
                );
                package.Save();
            }
        }

        /// <summary>
        /// Exports the given schedules to a single Excel workbook named after the Revit project plus " nullCarbon export",
        /// then deletes any temporary CSV/TXT files that Revit generates during export.
        /// </summary>
        /// <param name="schedules">The list of schedules to export.</param>
        /// <param name="exportPath">The directory to export to.</param>
        /// <returns>A message summarizing the export results.</returns>
        public static string Export(List<Schedule> schedules, string exportPath)
        {
            // If nothing is selected or no schedules found, bail out early.
            if (schedules == null || schedules.Count == 0)
            {
                return "No schedules to export.";
            }

            // Attempt to get the Revit project's title from the first schedule.
            // Fall back if it's somehow null/empty.
            string docTitle = schedules[0]?.RevitViewSchedule?.Document?.Title;
            if (string.IsNullOrWhiteSpace(docTitle))
            {
                docTitle = "UnknownProject";
            }

            // Build the final Excel export file name:
            string mergedExcelFileName = docTitle + " nullCarbon-LCA-Export.xlsx";
            string mergedExcelFilePath = Path.Combine(exportPath, mergedExcelFileName);

            StringBuilder exportMsg = new StringBuilder();
            int successes = 0;
            int attempts = 0;

            // Create a new ExcelPackage for the final merged Excel file.
            using (ExcelPackage mergedPackage = new ExcelPackage())
            {
                using (var options = LoadSavedExportOptions())
                {
                    foreach (var schedule in schedules)
                    {
                        attempts++;

                        // Make sure the export path actually exists.
                        if (!Directory.Exists(exportPath))
                        {
                            exportMsg.AppendLine($"[Error] {schedule.ExportName}. Directory not found: {exportPath}");
                            continue;
                        }

                        // Export the schedule to a temporary CSV/TXT file via Revit's built-in "schedule.Export" method.
                        if (schedule.Export(options, exportPath))
                        {
                            string csvFilePath = Path.Combine(exportPath, schedule.ExportName);
                            try
                            {
                                // Use the name (minus extension) as the worksheet name.
                                string sheetName = Path.GetFileNameWithoutExtension(schedule.ExportName);

                                // Create a new worksheet in the merged Excel file.
                                ExcelWorksheet worksheet = mergedPackage.Workbook.Worksheets.Add(sheetName);

                                bool firstRowIsHeader = false;

                                // Use the user-selected field delimiter from options.
                                // (At this point, options.FieldDelimiter should already be the actual character
                                //  e.g. "\t" or "," or ";".)
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

                                // Read the CSV file from disk, then load into the worksheet.
                                string csvContent = File.ReadAllText(csvFilePath);
                                worksheet.Cells["A1"].LoadFromText(
                                    csvContent,
                                    format,
                                    OfficeOpenXml.Table.TableStyles.Medium27,
                                    firstRowIsHeader
                                );

                                exportMsg.AppendLine($"[Success] {schedule.ExportName}");
                                successes++;
                            }
                            catch (Exception ex)
                            {
                                exportMsg.AppendLine($"[Error] Merging {schedule.ExportName}: {ex.Message}");
                            }
                            finally
                            {
                                // Delete the temporary CSV/TXT file so only the single merged Excel remains.
                                if (File.Exists(csvFilePath))
                                {
                                    File.Delete(csvFilePath);
                                }
                            }
                        }
                        else
                        {
                            exportMsg.AppendLine($"[Error] {schedule.ExportName}");
                        }
                    }
                }

                // If the merged Excel file already exists, delete it to avoid conflicts.
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

            // We only keep the single Excel file, no CSV/TXT are retained.
            exportMsg.AppendLine($"[Excel Export] Merged Excel file created at {mergedExcelFilePath}");

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
                    {
                        continue;
                    }
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
            // Key = Display text, Value = Actual delimiter string
            return new Dictionary<string, string>
            {
                { "Comma", "," },
                { "Semi-Colon", ";" },
                { "Tab", "\t" },
                { "Space", " " }
            };
        }

        /// <summary>
        /// Returns a dictionary of available text qualifiers.
        /// </summary>
        public static Dictionary<string, string> GetFieldTextQualifiers()
        {
            // Key = Display text, Value = Actual qualifier string
            return new Dictionary<string, string>
            {
                { "Double Quote (\")", "\"" },
                { "Quote (\')", "\'" },
                { "None", string.Empty }
            };
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

            // Grab what's stored in Settings for the delimiter
            string fieldDelim = Settings.Default.FieldDelimiter;

            // Handle various representations of tab character
            if (string.IsNullOrWhiteSpace(fieldDelim))
            {
                fieldDelim = "\t";
            }
            else if (fieldDelim.Equals("tab", StringComparison.OrdinalIgnoreCase)
                  || fieldDelim.Equals("/tab", StringComparison.OrdinalIgnoreCase)
                  || fieldDelim.Equals("\\t", StringComparison.OrdinalIgnoreCase)
                  || fieldDelim.Equals(@"\t", StringComparison.OrdinalIgnoreCase))
            {
                fieldDelim = "\t";
            }

            // Process escape sequences in the delimiter string
            fieldDelim = ProcessEscapeSequences(fieldDelim);

            // Now set the final delimiter
            options.FieldDelimiter = fieldDelim;

            // Whether to export grouped headers/footers, etc.
            options.HeadersFootersBlanks = Settings.Default.ExportGrouppHeaderAndFooters;

            // Handle text qualifier from user settings
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
            else
            {
                // Default to DoubleQuote if not recognized
                options.TextQualifier = ExportTextQualifier.DoubleQuote;
            }

            // ExportTitle is a bool in your Settings
            options.Title = Settings.Default.ExportTitle;

            return options;
        }

        /// <summary>
        /// Helper method to process escape sequences in strings
        /// </summary>
        private static string ProcessEscapeSequences(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Replace common escape sequences
            return input
                .Replace("\\t", "\t")  // Tab
                .Replace("\\n", "\n")  // Newline
                .Replace("\\r", "\r"); // Carriage return
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
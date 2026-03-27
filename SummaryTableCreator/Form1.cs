using SummaryTableCreator.Importer;
using SummaryTableCreator.Models;
using SummaryTableCreator.Parser;
using SummaryTableCreator.Reports;
using SummaryTableCreator.UI;

namespace SummaryTableCreator
{
    public partial class Form1 : Form
    {
        private readonly WordImporter _importer = new WordImporter();
        private readonly HeadingParser _parser;
        private List<HeadingResult>? _results;
        public Form1()
        {
            InitializeComponent();
            Logger.Initialize(rtb_log);
            _parser = new HeadingParser();  // Ensure parser is instantiated
            lbl_section_start.Enabled = false; // Disable section start label
            nud_report_section.Enabled = false; // Disable numeric up-down for section selection
            btn_extract.Enabled = false; // Disable extract button until import
            cb_cvss.Enabled = false; // Disable CVSS checkbox until extraction
            //cb_cvss_score.Enabled = false; // Disable CVSS score checkbox until extraction
            cb_interim_excel.Enabled = false; // Disabled interim Excel checkbox by default
            cb_recommendations.Enabled = false; // Disable recommendations checkbox until extraction
            btn_generate_table.Enabled = false;

            // Setup tooltips
            SetupTooltips();
        }

        private void SetupTooltips()
        {
            // Configure tooltip behavior
            toolTip1.AutoPopDelay = 5000;  // Show for 5 seconds
            toolTip1.InitialDelay = 1000;  // Wait 1 second before showing
            toolTip1.ReshowDelay = 500;    // Wait 0.5 seconds between tooltips
            toolTip1.ShowAlways = true;    // Show even when form is not active

            // Set tooltips for each control
            toolTip1.SetToolTip(btn_import, "Select and import a Word document (.docx) for processing");
            toolTip1.SetToolTip(nud_report_section, "Choose which report section to parse (e.g., section 6)");
            toolTip1.SetToolTip(btn_extract, "Extract headings and issues from the selected section");
            toolTip1.SetToolTip(cb_cvss, "Commercial format by default, enable to switch to CHECK format");
            //toolTip1.SetToolTip(cb_cvss_score, "Show numerical CVSS scores for later report compilation");
            toolTip1.SetToolTip(cb_recommendations, "Populate the Issue Descriptions column with data from the JSON database");
            toolTip1.SetToolTip(btn_generate_table, "Outputs the summary tables in new documents in the same folder");
            toolTip1.SetToolTip(cb_interim_excel, "Outputs the summary table in Excel format with phases split on sheets");
        }

        private void btn_import_Click(object sender, EventArgs e)
        {
            Logger.Clear();
            lbl_section_start.Enabled = false; // Disable section start label
            nud_report_section.Enabled = false; // Disable numeric up-down for section selection
            btn_extract.Enabled = false; // Disable extract button until import
            cb_cvss.Enabled = false; // Disable CVSS checkbox until extraction
            //cb_cvss_score.Enabled = false; // Disable CVSS score checkbox until extraction
            cb_interim_excel.Enabled = false; // Disabled interim Excel checkbox by default
            cb_recommendations.Enabled = false; // Disable recommendations checkbox until extraction
            btn_generate_table.Enabled = false;
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "Word Documents|*.docx";
                dlg.Title = "Select a Word Document";

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    string path = dlg.FileName;
                    lbl_filepath.Text = path;
                    try
                    {
                        // Logger.Info("[+] Selected file: " + path);
                        _importer.Import(path);
                        Logger.Okay("\n[+] Import completed");
                        lbl_section_start.Enabled = true;
                        nud_report_section.Enabled = true;
                        btn_extract.Enabled = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("\n[!] Import failed: " + ex.Message);
                    }
                }
                else
                {
                    Logger.Warning("\n[!] File selection cancelled.");
                }
            }
        }

        private void btn_extract_Click(object? sender, EventArgs e)
        {
            Logger.Clear(); // Clear log before extraction
            try
            {
                // Assuming lbl_filepath holds the selected file path
                string filePath = lbl_filepath.Text.Trim();
                int sectionLevel = (int)nud_report_section.Value;
                bool includeRecommendations = cb_recommendations.Checked;
                Logger.Info($"[+] Extracting section {sectionLevel} from {filePath}");

                _results = _parser.Parse(filePath, sectionLevel, includeRecommendations).ToList();
                // Count phases and issues separately
                var phases = _results.Where(r => r.IsPhaseHeader).Count();
                var issues = _results.Where(r => !r.IsPhaseHeader).Count();

                Logger.Okay($"\n[+] Found {phases} Phases");
                Logger.Okay($"[+] Found {issues} Issues\n");

                //Logger.Okay($"\n[+] Parsed {_results.Count} headings\n");


                btn_generate_table.Enabled = _results.Any();
                cb_cvss.Enabled = true; // Enable CVSS checkbox
                //cb_cvss_score.Enabled = true; // Enable CVSS score checkbox
                cb_interim_excel.Enabled = true; // Enable interim Excel checkbox by default
                cb_recommendations.Enabled = true; // Enable recommendations checkbox
            }
            catch (Exception ex)
            {
                Logger.Error("[!] Parsing failed: Have you imported a document?");
                // Show the actual exception details instead of generic message
                Logger.Error($"[!] Parsing failed: {ex.Message}");
                Logger.Error($"[!] Exception type: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Logger.Error($"[!] Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        private void btn_generate_table_Click(object? sender, EventArgs e)
        {
            if (_results == null || !_results.Any())
            {
                Logger.Warning("[!] No results to generate report from.");
                return;
            }

            try
            {
                bool generateExcel = cb_interim_excel.Checked;
                bool useSingleLevel = cb_cvss.Checked; // CHECK reporting mode
                bool includeRecommendations = cb_recommendations.Checked;

                if (generateExcel)
                {
                    // Generate Excel report
                    bool showCvssScore = cb_cvss_score.Checked;
                    ExcelReportGenerator.Generate(lbl_filepath.Text, _results, useSingleLevel, showCvssScore, includeRecommendations);
                }
                else
                {
                    // Generate new consolidated Word report
                    FindingsSummaryReportGenerator.Generate(lbl_filepath.Text, _results, useSingleLevel, includeRecommendations);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("[!] Failed to generate report: " + ex.Message);
                // Log stack trace for debugging
                Logger.Error($"[!] Stack trace: {ex.StackTrace}");
            }
        }
    }
}
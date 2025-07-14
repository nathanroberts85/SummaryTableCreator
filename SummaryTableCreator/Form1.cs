using SummaryTableCreator.Importer;
using SummaryTableCreator.Models;
using SummaryTableCreator.Parser;
using SummaryTableCreator.UI;
using SummaryTableCreator.Reports;
using System.IO;

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
            btn_generate_table.Enabled = false;
            lbl_section_start.Enabled = false;
            btn_extract.Enabled = false;
            nud_report_section.Enabled = false;
            cb_cvss.Enabled = false;
            cb_cvss_score.Enabled = false;
        }

        private void btn_import_Click(object sender, EventArgs e)
        {
            // Clear previous results and log
            _results = null;
            lbl_filepath.Text = string.Empty;
            Logger.Clear();
            btn_extract.Enabled = false;
            lbl_section_start.Enabled = false;
            btn_generate_table.Enabled = false;
            nud_report_section.Enabled = false;
            cb_cvss.Enabled = false;
            cb_cvss_score.Enabled = false;
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
                        //Logger.Info("Selected file: " + path);
                        _importer.Import(path);
                        Logger.Okay("[+] Import completed.");
                        btn_extract.Enabled = true;
                        lbl_section_start.Enabled = true;
                        nud_report_section.Enabled = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("[!] Import failed: " + ex.Message);
                    }
                }
                else
                {
                    Logger.Warning("[!] File selection cancelled.");
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
                Logger.Info($"[+] Extracting section {sectionLevel} from {filePath}");

                _results = _parser.Parse(filePath, sectionLevel).ToList();
                Logger.Okay($"\n[+] Parsed {_results.Count} headings\n");

                // Detailed logging of each heading (for testing/debugging)
                //foreach (var result in _results)
                //{
                //    Logger.Info(
                //        $"Found heading: Ref={result.Reference}, Issue='{result.Issue}', CVSS={(result.CvssScore.HasValue ? result.CvssScore.Value.ToString() : "N/A")}"
                //    );
                //}

                btn_generate_table.Enabled = _results.Any();
                cb_cvss.Enabled = true; // Enable CVSS checkbox
                cb_cvss_score.Enabled = true; // Enable CVSS score checkbox
            }
            catch (Exception ex)
            {
                Logger.Error("[!] Parsing failed: Have you imported a document?");
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
                // Use template-based generation (assuming simplified rating for now)
                bool useSingleLevel = cb_cvss.Checked; // You can add a checkbox for this later
                bool showCvssScore = cb_cvss_score.Checked; // Read from checkbox to show CVSS scores in brackets
                SummaryReportGenerator.Generate(lbl_filepath.Text, _results, useSingleLevel, showCvssScore);
                //Logger.Okay("[+] Summary table generated successfully using template.");
            }
            catch (Exception ex)
            {
                Logger.Error("[!] Failed to generate summary table: " + ex.Message);
            }
        }

        private void cb_cvss_score_CheckedChanged(object sender, EventArgs e)
        {

        }
    }
}
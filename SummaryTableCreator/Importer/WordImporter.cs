using SummaryTableCreator.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SummaryTableCreator.Importer
{
    public class WordImporter
    {
        /// <summary>
        /// Imports the specified Word document.
        /// </summary>
        /// <param name="filePath">Full path to the .docx file.</param>
        public void Import(string filePath)
        {
            // TODO: Implement Word document parsing logic
            Logger.Info($"[+] Importing document: {filePath}");
        }
    }
}

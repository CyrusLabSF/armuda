using System;
using System.IO;
using OfficeOpenXml;
using UnityEngine;

public class BeliefExportManager : MonoBehaviour
{
    public ArTusBeliefEngine beliefEngine;
    private string exportPath = "D:/ArTusCloud-Deployment/UNIVERcity/Exports/Beliefs/BeliefExport.xlsx";

    [ContextMenu("Export Beliefs to XLSX")]
    public void ExportBeliefsToExcel()
    {
        if (beliefEngine == null || beliefEngine.beliefs == null)
        {
            Debug.LogWarning("[BeliefExport] Belief engine is null. Export aborted.");
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(exportPath));

            FileInfo fileInfo = new FileInfo(exportPath);
            if (fileInfo.Exists)
                fileInfo.Delete();

            using var package = new ExcelPackage(fileInfo); // 🚫 No LicenseContext line
            var sheet = package.Workbook.Worksheets.Add("Beliefs");

            sheet.Cells[1, 1].Value = "Belief";
            sheet.Cells[1, 2].Value = "Confidence";
            sheet.Cells[1, 3].Value = "Domain";
            sheet.Cells[1, 4].Value = "Emotion";
            sheet.Cells[1, 5].Value = "Updated";
            sheet.Cells[1, 6].Value = "Contradiction?";

            int row = 2;
            foreach (var kvp in beliefEngine.beliefs)
            {
                var belief = kvp.Value;
                sheet.Cells[row, 1].Value = belief.belief;
                sheet.Cells[row, 2].Value = belief.confidenceScore;
                sheet.Cells[row, 3].Value = belief.domain;
                sheet.Cells[row, 4].Value = belief.dominantEmotion;
                sheet.Cells[row, 5].Value = belief.lastUpdated;
                sheet.Cells[row, 6].Value = belief.isFlaggedContradiction ? "Yes" : "No";
                row++;
            }

            package.Save();
            Debug.Log($"[BeliefExport] ✅ XLSX saved to: {exportPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BeliefExport] ❌ Failed to export: {ex.Message}");
        }
    }
}

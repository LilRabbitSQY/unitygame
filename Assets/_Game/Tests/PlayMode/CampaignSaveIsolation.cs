using System;
using System.IO;
using IOPath = System.IO.Path;
using NUnit.Framework;
using UnityEngine;

namespace FinalDefense.Tests
{
    // A copied Unity project still shares macOS PlayerPrefs with the original
    // product. Preserve exactly the campaign key touched by these integration
    // tests, including an on-disk backup for recovery after an editor crash.
    [SetUpFixture]
    public sealed class CampaignSaveIsolation
    {
        private const string Key = "FinalDefense.Campaign.v1";
        private bool existed;
        private string value;
        private string backup;
        private bool preserved;

        [Serializable]
        private sealed class Backup { public bool existed; public string value; }

        [OneTimeSetUp]
        public void PreserveCampaign()
        {
            string expectedCompany=Environment.GetEnvironmentVariable("FINAL_DEFENSE_TEST_COMPANY");
            string expectedProduct=Environment.GetEnvironmentVariable("FINAL_DEFENSE_TEST_PRODUCT");
            if(!string.IsNullOrEmpty(expectedCompany)) Assert.That(Application.companyName,Is.EqualTo(expectedCompany),"Use the isolated test preference domain before touching any campaign key");
            if(!string.IsNullOrEmpty(expectedProduct)) Assert.That(Application.productName,Is.EqualTo(expectedProduct),"Use the isolated test preference domain before touching any campaign key");
            // A second Unity process receives the exact bytes exported by the
            // first process's real UI journey; no campaign values are fabricated.
            string resumePath=Environment.GetEnvironmentVariable("FINAL_DEFENSE_RESUME_SAVE_PATH");
            string resumedValue=string.IsNullOrEmpty(resumePath)?null:File.ReadAllText(resumePath);
            existed = PlayerPrefs.HasKey(Key);
            value = existed ? PlayerPrefs.GetString(Key) : "";
            string folder = Environment.GetEnvironmentVariable("FINAL_DEFENSE_PREFS_BACKUP_DIR");
            if (string.IsNullOrEmpty(folder)) folder = IOPath.GetTempPath();
            Directory.CreateDirectory(folder);
            backup = IOPath.Combine(folder, "final-defense-campaign-backup-" + DateTime.UtcNow.Ticks + ".json");
            File.WriteAllText(backup, JsonUtility.ToJson(new Backup { existed = existed, value = value }));
            preserved=true;
            PlayerPrefs.DeleteKey(Key);
            if(resumedValue!=null) PlayerPrefs.SetString(Key,resumedValue);
            PlayerPrefs.Save();
        }

        [OneTimeTearDown]
        public void RestoreCampaign()
        {
            if(!preserved)return;
            if (existed) PlayerPrefs.SetString(Key, value); else PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            if (!string.IsNullOrEmpty(backup) && File.Exists(backup)) File.Delete(backup);
        }
    }
}

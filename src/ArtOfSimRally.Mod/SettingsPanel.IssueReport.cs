using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Dbce.Wheel.Support;
using UnityEngine;

namespace ArtOfSimRally.Mod
{
    internal static partial class SettingsPanel
    {
        private static ReportProduct _issueProduct;
        private static bool _issueOpen, _issueUmmLog, _issueFfbLog, _issuePlayerLog, _issueReviewed;
        private static string _issueTitle = "", _issueDescription = "", _issueStatus = "", _issueExport;
        private static PreparedReport _issuePrepared;
        private static Vector2 _issuePreviewScroll;
        private static GUIStyle _issuePreviewStyle;
        private static GUISkin _issuePreviewSkin;

        private static ReportProduct IssueProduct()
        {
            if (_issueProduct == null)
            {
                var version = (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                    typeof(SettingsPanel).Assembly, typeof(AssemblyInformationalVersionAttribute));
                _issueProduct = new ReportProduct("art of rally", version?.InformationalVersion ?? Main.ModVersion,
                    "d-b-c-e/dbce-mods-art-of-rally");
            }
            return _issueProduct;
        }

        private static void IssueReportHelp()
        {
            Heading("Help on GitHub");
            if (RightButton("Submit GitHub Issue", 190)) _issueOpen = true;
            if (W.Link("help-mod-github", "Mod on GitHub")) OpenIssueUrl(IssueProduct().ModPage);
            if (W.Link("help-wheelkit-github", "Wheelkit on GitHub")) OpenIssueUrl(IssueProduct().WheelkitPage);
            if (!_issueOpen) { Help(_issueStatus); return; }

            string title = Field("issue-title", "Issue title", _issueTitle);
            W.Caption("What happened and how to reproduce it");
            GUI.SetNextControlName("issue-description");
            W.Register("issue-description");
            string description = GUILayout.TextArea(_issueDescription, GUILayout.Height(100 * SettingsPresentation.Scale),
                GUILayout.Width(SettingsPresentation.BodyWidth));
            W.Reveal("issue-description");
            if (title != _issueTitle || description != _issueDescription)
            {
                _issueTitle = title; _issueDescription = description; InvalidateIssueReview();
            }
            Help("Logs are optional. Only the logs you select are read when you choose Review report.");
            IssueLogChoice(ref _issueUmmLog, "Include mod manager log");
            IssueLogChoice(ref _issueFfbLog, "Include force feedback log");
            IssueLogChoice(ref _issuePlayerLog, "Include Unity player log");
            Help("Redaction is best effort. Review the text. Edit your description or deselect a log and review again if needed.");

            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !GameState.IsDriving;
            if (RightButton("Review report", 150)) PrepareIssueReport();
            GUI.enabled = enabled;
            if (GameState.IsDriving) Help("Pause before preparing a report or opening a GitHub draft.");
            if (_issuePrepared != null)
            {
                W.Caption("Report preview");
                _issuePreviewScroll = GUILayout.BeginScrollView(_issuePreviewScroll,
                    GUILayout.Height(200 * SettingsPresentation.Scale), GUILayout.Width(SettingsPresentation.BodyWidth));
                if (_issuePreviewStyle == null || _issuePreviewSkin != GUI.skin)
                {
                    _issuePreviewSkin = GUI.skin;
                    _issuePreviewStyle = new GUIStyle(GUI.skin.label) { richText = false, wordWrap = true };
                }
                // Leave room for the inner scrollbar without changing the shared widget body width.
                GUILayout.Label(_issuePrepared.Preview, _issuePreviewStyle, GUILayout.Width(Math.Max(120,
                    SettingsPresentation.BodyWidth - 24 * SettingsPresentation.Scale)));
                GUILayout.EndScrollView();
                _issueReviewed = Toggle(_issueReviewed, "I reviewed this report");
                Help("Opening the draft sends its prefilled text to GitHub. You check and submit the issue there.");
                Help("Long reports save one reviewed text file on your Desktop for manual attachment. Attaching uploads the file immediately.");
                GUI.enabled = enabled && _issueReviewed && !GameState.IsDriving;
                if (RightButton("Open reviewed GitHub draft", 250)) OpenPreparedIssue();
                GUI.enabled = enabled;
            }
            if (RightButton("Close issue form", 160)) _issueOpen = false;
            Help(_issueStatus);
            if (_issueExport != null) Help("Reviewed report: " + _issueExport);
        }

        private static void IssueLogChoice(ref bool selected, string label)
        {
            bool choice = Toggle(selected, label);
            if (choice != selected) { selected = choice; InvalidateIssueReview(); }
        }

        private static void InvalidateIssueReview()
        {
            _issuePrepared = null; _issueReviewed = false; _issueExport = null; _issueStatus = "";
            _issuePreviewScroll = Vector2.zero;
        }

        private static void PrepareIssueReport()
        {
            // No settings/binding/save inspection and no broad SupportBundle collection. These are the same
            // three known text-log locations already used by Art's support diagnostics, explicitly selected here.
            InvalidateIssueReview();
            try
            {
                var logs = new List<string>();
                if (_issueUmmLog) logs.Add(Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "",
                    @"artofrally_Data\Managed\UnityModManager\Log.txt"));
                if (_issueFfbLog) logs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ArtOfSimRally", "ffb.log"));
                if (_issuePlayerLog) logs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    @"AppData\LocalLow\Funselektor Labs\Art of Rally\Player.log"));
                _issuePrepared = SupportReport.Prepare(IssueProduct(), _issueTitle, _issueDescription, logs);
            }
            catch (Exception ex) { _issueStatus = "Could not prepare the report: " + ex.Message; }
        }

        private static void OpenPreparedIssue()
        {
            if (_issuePrepared == null || !_issueReviewed) return;
            try
            {
                var submission = _issuePrepared.CreateBrowserSubmission(_issueReviewed);
                if (submission.NeedsAttachment && _issueExport == null)
                {
                    // This explicit reviewed action exports one immutable snapshot, without rereading any log.
                    // A unique report folder keeps the expected attachment filename without replacing owner files.
                    string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                        "ArtOfSimRally-report-" + Guid.NewGuid().ToString("N"));
                    SupportReport.RefuseLinks(Path.GetDirectoryName(folder));
                    Directory.CreateDirectory(folder);
                    string report = Path.Combine(folder, "support-report.txt");
                    _issuePrepared.WriteReviewFile(report);
                    _issueExport = report;
                }
                _issueStatus = submission.Instructions;
                Application.OpenURL(submission.Url.AbsoluteUri);
            }
            catch (Exception ex) { _issueStatus = "Could not open the GitHub draft: " + ex.Message; }
        }

        private static void OpenIssueUrl(Uri url)
        {
            try { Application.OpenURL(url.AbsoluteUri); }
            catch (Exception ex) { _issueStatus = "Could not open the link: " + ex.Message; }
        }
    }
}

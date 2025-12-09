using Godot;
using System;




namespace OpenAmi.Scripts
{
    public partial class AmiDisplay : Window
    {
        private AmiMain _amiMain;

        public RichTextLabel _displayLabel;
        public Button _exportButton;

        public string _filename = "";

        public override void _Ready()
        {
            try
            {
                _amiMain = this.GetParent<AmiMain>();
                _displayLabel = GetNode<RichTextLabel>("DisplayPanel/DisplayTopContainer/DisplayLabel");
                _exportButton = GetNode<Button>("DisplayPanel/DisplayTopContainer/DisplayButtonsContainer/ExportButton");
                //GD.Print("AmiDisplay:_Ready:  Display Window loaded successfully in Visual Studio.");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Display._Ready - Error during initialization: {ex.Message}");
            }

            //GD.Print("Display._Ready - Display Window initialized.");

        }

        //Export Button Handler
        private void OnExportButtonPressed()
        {
            _displayLabel.SelectAll();
            var selectedText = _displayLabel.GetSelectedText();
            if (!string.IsNullOrEmpty(selectedText))
            {
                try
                {
                    var startDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
                    var fileDialog = new FileDialog
                    {
                        FileMode = FileDialog.FileModeEnum.SaveFile,
                        Access = FileDialog.AccessEnum.Filesystem,
                        UseNativeDialog = true,
                        CurrentDir = startDir,
                        CurrentFile = _amiMain._viewFileName,

                    };
                    fileDialog.FileSelected += path =>
                    {
                        try
                        {
                            GD.Print($"AmiDisplay.OnExportButtonPressed - exporting {_amiMain._viewFileName} to {fileDialog.CurrentFile}");
                            string copyFromPath = _amiMain._viewFilePath;
                            string copyFromFile = _amiMain._viewFilePath + _amiMain._viewFileName;
                            GD.Print($"AmiDisplay.OnExportButtonPressed - exporting {copyFromFile} to {path}");
                            System.IO.File.Copy(copyFromFile, path, true);
                            GD.Print($"AmiMain:OnExportButtonPressed - file exported to {path}");
                        }
                        catch (Exception e)
                        {
                            GD.PrintErr($"AmiMain:OnExportButtonPressed - Error writing to file: {e.Message}");
                        }
                    };
                    AddChild(fileDialog);
                    fileDialog.PopupCentered();
                }
                catch (Exception e)
                {
                    GD.PrintErr($"AmiMain:OnExportButtonPressed - Error: Export failed: {e.Message}");
                }
            }
            else
            {
                GD.PrintErr($"AmiMain:OnExportButtonPressed - Error: No text selected in DisplayLabel");
            }
        }

        //Copy Button Handler
        private void OnCopyButtonPressed()
        {
            _displayLabel.SelectAll();
            var selectedText = _displayLabel.GetSelectedText();
            if (!string.IsNullOrEmpty(selectedText))
            {
                try
                {
                    DisplayServer.ClipboardSet(selectedText);
                    GD.Print($"AmiMain:OnEditButtonItemPressed - Copied text from DisplayLabel");
                }
                catch (Exception e)
                {
                    GD.PrintErr($"AmiMain:OnEditButtonItemPressed - Error: Clipboard access failed: {e.Message}");
                }
            }
            else
            {
                GD.PrintErr($"AmiMain:OnEditButtonItemPressed - Error: No text selected in DisplayLabel");
            }

        }
        
        //Done button handler
        private void OnDoneButtonPressed()
        {
            _amiMain.CloseDisplayWindow();
            GD.Print($"Display.cs.OnDoneButtonPressed: Done button pressed. Display Window visibilty off.");

        }
    }
}
using OpenAmi.Scripts;
using Godot;
using System;
using System.Security.Cryptography;
using System.Text;


namespace OpenAmi.Scripts
{

    public partial class AmiTextEntry : ConfirmationDialog
    {
        private AmiMain _amiMain;
        private AppManager _appManager;
        
        private Label _textInstructionLabel;
        private Label _lineLabel1;
        private Label _lineLabel2;
        private LineEdit _lineEdit1;
        private LineEdit _lineEdit2;

        private int _useCase = -1;
        private int _retryCount = 0;

        public override void _Ready()
        {
            try
            {
                //Initialize fields
                _amiMain = (AmiMain)this.GetParent();
                _appManager = GetNode<AppManager>("/root/AppManager");

                _textInstructionLabel = GetNode<Label>("TextEntryCont/TextInstructionLabel");
                _lineLabel1 = GetNode<Label>("TextEntryCont/StackCont/Line1Cont/LineLabel1");
                _lineLabel2 = GetNode<Label>("TextEntryCont/StackCont/Line2Cont/LineLabel2");
                _lineEdit1 = GetNode<LineEdit>("TextEntryCont/StackCont/Line1Cont/LineEdit1");
                _lineEdit2 = GetNode<LineEdit>("TextEntryCont/StackCont/Line2Cont/LineEdit2");
               //GD.Print("AMI_TextEntry initialized.");

            }
            catch (Exception e)
            {
                GD.PrintErr("AMI_TextEntry initialzation failed: ", e);
            }

        }

        public void CallTextEntryByUseCase(int useCase)

        //Caller enum: 0 = UpdateButton, 1 = Update API Key, 2 = FontSize, 9 = Update CollectionID
        {
            try
            {
                _useCase = useCase;
                switch (useCase)
                {
                    case 0: //Update spend
                        _textInstructionLabel.Text = "Set budget and threshold:";
                        _lineLabel1.Visible = true;
                        _lineLabel2.Visible = true;
                        _lineLabel1.Text = "Budget:";
                        _lineLabel2.Text = "Threshold:";
                        _lineEdit1.Visible = true;
                        _lineEdit2.Visible = true;
                        _lineEdit1.SetCustomMinimumSize(new(120, 40));
                        _lineEdit2.SetCustomMinimumSize(new(120, 40));
                        _lineEdit1.Text = _appManager._config["Budget"].ToString();
                        _lineEdit2.Text = _appManager._config["Threshold"].ToString();
                        _lineEdit2.Visible = true;
                        Visible = true;
                        break;

                    case 1: //Update API Key
                        _textInstructionLabel.Text = "Enter your new API key:";
                        _lineLabel1.Visible = false;
                        _lineLabel2.Visible = false;
                        _lineEdit2.Visible = false;
                        _lineEdit1.SetCustomMinimumSize(new(720, 40));
                        Visible = true;
                        break;

                    case 2: //Set Default Font Size
                        _textInstructionLabel.Text = "Enter new font size in pixels (12-56):";
                        _lineLabel1.Visible = false;
                        _lineLabel2.Visible = false;
                        _lineEdit1.SetCustomMinimumSize(new(120, 40));
                        _lineEdit1.Text = "36";
                        if (_appManager._config.TryGetValue("FontSize", out var size))
                            _lineEdit1.Text = size.ToString();
                        _lineEdit2.Visible = false;
                        this.Visible = true;
                        break;

                    case 9: //update CollectionID
                        _textInstructionLabel.Text = "Input the Collection ID for the Collection Search Tool:";
                        _lineLabel1.Visible = false;
                        _lineLabel2.Visible = false;
                        _lineEdit1.SetCustomMinimumSize(new(280, 40));
                        _lineEdit1.Secret = false;
                        _lineEdit2.Visible = false;
                        Transient = true;
                        Exclusive = false;
                        Visible = true;

                        break;
                }
            }
            catch (Exception e)
            {
                GD.PrintErr("AMI_TextEntry call failed: ", e);
            }
        }

        private void OnConfirmed()
        {
    
            switch (_useCase)
            {
                case 0: //Update spend
                    
                    var budget = _lineEdit1.Text.ToFloat();
                    var threshold = _lineEdit2.Text.ToFloat();
                    if (budget > threshold)
                    {
                        _appManager._config["Budget"] = _lineEdit1.Text;
                        _appManager._config["Threshold"] = _lineEdit2.Text;
                        _appManager._config["RunningSpend"] = "00.0000";
                        var newBudgetSTR = $"{_appManager._config["Budget"]:c4}";
                        var newThresholdSTR = $"{_appManager._config["Threshold"]:c4}";
                        var newRunningSpendSTR = $"{_appManager._config["RunningSpend"]:c4}";
                        _amiMain._budgetLabel.Text = newBudgetSTR;
                        _amiMain._runningLabel.Text = newRunningSpendSTR;
                        GD.Print($"AMI_TextEntry: Budget {newBudgetSTR}, Threshold {newThresholdSTR}, Running Spend {newRunningSpendSTR}");
                        _lineEdit1.Clear();
                        _lineEdit2.Clear();
                        Visible = false;
                        
                    }
                    else
                    {
                        _lineEdit1.Text = _appManager._config["Budget"].ToString();
                        _lineEdit2.Text = _appManager._config["Threshold"].ToString();
                        _textInstructionLabel.Text = "Budget must be greater than threshold.";
                        Visible = true;
                    }
                        break;

                case 1: //Update API Key
                    _textInstructionLabel.Text = "Enter your new API key:";
                    // Select and retrieve API key from UserInputTextEdit
                    _lineEdit1.SelectAll();
                    string apiKey = _lineEdit1.GetSelectedText();

                    // Validate input
                    if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length < 17)
                    {
                        GD.PrintErr("AmiMain:OnSendButtonPressed - Error: Invalid API key input");
                        _textInstructionLabel.Text = "Invalid API key. Please retry.";
                        _lineEdit1.Clear();
                        return;
                    }
                    else
                    {
                        // Encode and save API key;
                        _appManager._apiKey = apiKey;
                        var apiKeyPath = "user://Context/cached.json";
                        var encodedKey = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(apiKey));
                        using var file = FileAccess.Open(apiKeyPath, FileAccess.ModeFlags.Write);
                        if (file != null)
                        {
                            file.StoreString(encodedKey);
                            _amiMain._switchIndex = 0; //Reset to user input mode
                            GD.Print("TextEntry: saved API key to file, switchIndex = 0");
                            _lineEdit1.Clear();
                            _amiMain._outputLabel.Text = "API key updated successfully.";
                            GD.Print("AmiTextEntry: - Saved API key");
                            Visible = false;
                        }
                        else
                        {
                            GD.PrintErr($"AmiMain:OnSendButtonPressed - Error: FileAccess null for {apiKeyPath}");
                            _amiMain._outputLabel.Text = "Error saving API key. Please try again.";
                            _lineEdit1.Clear();
                        }
                    }
                    break;

                case 2: //Set I/O Font Size
                    _textInstructionLabel.Text = "Enter new font size in pixels (12-56):";
                    //Validate input
                    var fontSize = _lineEdit1.Text.ToInt();
                    try
                    {
                        if (fontSize > 11 && fontSize < 57)
                        {
                            try
                            {
                                _appManager._config["FontSize"] = fontSize;
                                _amiMain.Theme.DefaultFontSize = fontSize;

                                _appManager.SaveConfig();

                                _lineEdit1.Clear();

                                GD.Print("Font Size changed");

                                Visible = false;


                            }
                            catch (Exception e)
                            {
                                GD.PrintErr("TextEntry: Font size change failed: ", e);
                            }
                        }
                        else
                        {
                            _textInstructionLabel.Text = "New font size must be between 12 and 56:";
                            _lineEdit1.Text = "36";
                            if (_appManager._config.TryGetValue("FontSize", out var size))
                                _lineEdit1.Text = size.ToString();
                            Visible = true;
                        }


                    }
                    catch (Exception e)
                    {
                        GD.PrintErr("TextEntry: Failed to set default font size: ",e);
                    }
                break;

                case 3: 
                    break;

                case 4: 
                    break;

                case 5: 
                    break;

                case 6: 
                    break;

                case 7: 
                    break;

                case 8: 
                    break;

                case 9: //check _lineEdit1 for text, if present, check for  "collection_", if present remove
                    // store remaining string to _appManager._config["CollectionID"], _appManager._SaveConfig(),
                    // reset TextEdit elements, set Visible = false

                    string collectionID = _lineEdit1.Text;
                    if (!string.IsNullOrWhiteSpace(collectionID))
                    {
                        if (collectionID.StartsWith("collection_"))
                        {
                            collectionID = collectionID["collection_".Length..];
                        }
                        _appManager._config["CollectionID"] = collectionID;
                        _appManager.SaveConfig();
                        GD.Print($"AmiTextEntry: Collection ID set to {collectionID}");
                        _amiMain._outputLabel.Text += "Collection ID updated successfully: " + collectionID;
                        _lineEdit1.Clear();
                        Visible = false;
                    }
                    else
                    {
                        _textInstructionLabel.Text = "Collection ID cannot be empty. Please enter again.";
                        _lineEdit1.Clear();
                        Visible = true;
                    }

                    break;

                default:
                    GD.PrintErr("AMI_TextEntry.OnConfirmed: Invalid use case.");
                    break;
            }

        }

        private void OnCanceled()
        {
            _lineEdit1.Clear();
            _lineEdit2.Clear();
            _lineEdit1.Secret = false;
            _lineEdit2.Secret = false;
            GD.Print($"TextEntry: Canceled for use case {_useCase}. Cleared inputs.");
        }

    }
}

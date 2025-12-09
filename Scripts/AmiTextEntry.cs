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

        [Signal] 
        public delegate void PasswordCompleteEventHandler();


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

        //Caller enum: 0 = UpdateButton, 1 = Update API Key, 2 = FontSize, 3 = Password Setting, 4 = Security question/answer setting, 
        // 5 = Password Entry, 6 = Security Answer Entry, 7 = API Key entry for lost password, 8 = Confirm password disable and API deletion

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

                    case 3: //Password Setting
                        _textInstructionLabel.Text = "Set your password (at least 8 characters, no spaces):";
                        _lineLabel1.Visible = true;
                        _lineLabel2.Visible = true;
                        _lineLabel1.Text = "Password:";
                        _lineLabel2.Text = "Confirm:";
                        _lineEdit1.Visible = true;
                        _lineEdit2.Visible = true;
                        _lineEdit1.SetCustomMinimumSize(new (280, 40));
                        _lineEdit2.SetCustomMinimumSize(new(280, 40));
                        Visible = true;
                        break;

                    case 4: //Set security question and answer
                        _textInstructionLabel.Text = "Define a security question and answer: (up to 60 characters):";
                        _lineLabel1.Text = "Question:";
                        _lineLabel2.Text = "Answer:";
                        _lineEdit1.SetCustomMinimumSize(new(720, 40));
                        _lineEdit1.SetCustomMinimumSize(new(720, 40));
                        Visible = true;
                        break;

                    default:
                        GD.PrintErr("AMI_TextEntry invalid caller index.");
                        break;

                    case 5: //Password Entry
                        _textInstructionLabel.Text = "Enter your password:";
                        _lineLabel1.Visible = false;
                        _lineLabel2.Visible = false;
                        _lineEdit1.SetCustomMinimumSize(new(280, 40));
                        _lineEdit1.Secret = true;
                        _lineEdit2.Visible = false;
                        Transient = true;
                        Exclusive = true;
                        Visible = true;

                        break;

                    case 6: // Security Answer Entry

                        string encodedQuestion = FileAccess.GetFileAsString("user://Assets/loc/71200e1b9b2258b4.json");
                        string plainQuestion = Encoding.UTF8.GetString(Convert.FromBase64String(encodedQuestion));
                        GD.Print("Security Question is ", plainQuestion);
                        _textInstructionLabel.Text = plainQuestion + "?";
                        _lineEdit1.Secret = false;
                        _lineEdit1.SetCustomMinimumSize(new(360, 40));

                        break;

                    case 7: // API Key entry for lost password

                        _textInstructionLabel.Text = "Enter your API key to verify identity:";
                        _lineEdit1.SetCustomMinimumSize(new(720, 40));
                        _lineEdit1.Secret = false;

                        break;

                    case 8: // Confirm password disable and API deletion if password, question, and API key fail
                       string instructionText = "Identity could not be verified. You may disable password protection and delete your API key. You won't be able to connect to Grok until you access your xAI console and get a new API key. Proceed? ";
                        _textInstructionLabel.Text = instructionText;
                        _lineEdit1.Visible = false;

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
                        var apiKeyPath = "user://Assets/loc/0cc27a50c7cc31eb.json";
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

                case 3: //Password Setting
                    try
                    {
                        // Step 1: Validate first password from _lineEdit1
                        string firstPassword = _lineEdit1.Text;
                        string confirmPassword = _lineEdit2.Text;
                        if (string.IsNullOrWhiteSpace(firstPassword) || firstPassword.Length < 8)
                        {
                            GD.PrintErr("AmiTextEntry.OnConfirmed case 3: First password invalid");
                            _textInstructionLabel.Text = "Password must be at least 8 characters with no spaces. Please enter again.";
                            _lineEdit1.Clear();
                            _lineEdit2.Clear();
                            return;
                        }

                        if (firstPassword != confirmPassword)
                        {
                            GD.PrintErr("AmiTextEntry.OnConfirmed case 3: Passwords do not match");
                            _textInstructionLabel.Text = "Passwords do not match. Please enter again.";
                            _lineEdit1.Clear();
                            _lineEdit2.Clear();
                            return;
                        }
                        GD.Print("AmiTextEntry.OnConfirmed case 3: First password accepted: ", firstPassword);

                        // Step 2: Hash and save password
                        string salt = GenerateSalt();
                        string hashedPassword = HashWithSalt(firstPassword, salt);
                        string passwordData = $"{salt}:{hashedPassword}";
                        string encodedPasswordData = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(passwordData));
                        string passwordFilePath = "user://Assets/loc/a2d6af3e2115a910.json";
                        using (var passwordFile = FileAccess.Open(passwordFilePath, FileAccess.ModeFlags.Write))
                        {
                            if (passwordFile != null)
                            {
                                passwordFile.StoreString(encodedPasswordData);
                                GD.Print("AmiTextEntry.OnConfirmed case 3: Hashed password saved");
                            }
                            else
                            {
                                GD.PrintErr("AmiTextEntry.OnConfirmed case 3: Failed to save hashed password");
                                _textInstructionLabel.Text = "Error saving password. Please try again.";
                                CallTextEntryByUseCase(3); // Re-iterate
                                return;
                            }
                        }

                        // Step 3: Clear and transition to case 4
                        _lineEdit1.Clear();
                        _lineEdit2.Clear();
                        CallTextEntryByUseCase(4);
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiTextEntry.OnConfirmed case 3: Exception: {e.Message}");
                        _textInstructionLabel.Text = "An error occurred. Please try again.";
                        CallTextEntryByUseCase(3); // Re-iterate
                    }
                    break;

                case 4: //Set security question and answer
                    try
                    {
                        // Validate security question from _lineEdit1
                        string securityQuestion = _lineEdit1.Text;
                        if (string.IsNullOrWhiteSpace(securityQuestion) || securityQuestion.Length > 60)
                        {
                            GD.PrintErr("AmiTextEntry.OnConfirmed case 4: Security question invalid");
                            _textInstructionLabel.Text = "Question must be 1-60 characters. Please enter again.";
                            _lineEdit1.Text = "";
                            return;
                        }

                        string securityAnswer = _lineEdit2.Text;
                        if (string.IsNullOrWhiteSpace(securityAnswer) || securityAnswer.Length > 60)
                        {
                            GD.PrintErr("AmiTextEntry.OnConfirmed case 4: Security answer invalid");
                            _textInstructionLabel.Text = "Answer must be 1-60 characters. Please enter again.";
                            _lineEdit2.Text = "";
                            return;
                        }

                        //Hash and save question and answer
                        string encodedQuestionData = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(securityQuestion));
                        string questionFilePath = "user://Assets/loc/71200e1b9b2258b4.json";
                        using (var questionFile = FileAccess.Open(questionFilePath, FileAccess.ModeFlags.Write))
                        {
                            if (questionFile != null)
                            {
                                questionFile.StoreString(encodedQuestionData);
                                GD.Print("AmiTextEntry.OnConfirmed case 4: Hashed security question saved");
                            }
                            else
                            {
                                GD.PrintErr("AmiTextEntry.OnConfirmed case 4: Failed to save security question");
                                _textInstructionLabel.Text = "Error saving question. Please try again.";
                                CallTextEntryByUseCase(4); // Re-iterate
                                return;
                            }
                        }

                        string answerSalt = GenerateSalt();
                        string hashedAnswer = HashWithSalt(securityAnswer, answerSalt);
                        string answerData = $"{answerSalt}:{hashedAnswer}";
                        string encodedAnswerData = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(answerData));
                        string answerFilePath = "user://Assets/loc/5852283cdccc2eb3.json";
                        using (var answerFile = FileAccess.Open(answerFilePath, FileAccess.ModeFlags.Write))
                        {
                            if (answerFile != null)
                            {
                                answerFile.StoreString(encodedAnswerData);
                                GD.Print("AmiTextEntry.OnConfirmed case 4: Hashed security answer saved");
                            }
                            else
                            {
                                GD.PrintErr("AmiTextEntry.OnConfirmed case 4: Failed to save security answer");
                                _textInstructionLabel.Text = "Error saving answer. Please try again.";
                                CallTextEntryByUseCase(4); // Re-iterate
                                return;
                            }
                        }

                        var locFile1 = Godot.FileAccess.Open("user://Assets/loc/8c2ad753bdd8f772.json", Godot.FileAccess.ModeFlags.Write);
                        locFile1.StoreString("Q2hlY2tmaWxlIGlzIEFzc2V0cy9sb2MvODQwZWM0NTM3ODk5YTFhMC5qc29u");

                        var locFile2 = Godot.FileAccess.Open("user://Assets/loc/09cf83c05d537cb5.json", Godot.FileAccess.ModeFlags.Write);
                        locFile2.StoreString("eyJwYXNzd29yZCI6ICI5Mzg5ZjI4MGQ2YjI4MzVjIn0");

                        //Clear and close (success; config set in Prefs)
                        _lineEdit1.Text = "";
                        _lineEdit2.Text = "";
                        Visible = false;

                        //Update menu check

                        _amiMain._preferencesButton.GetPopup().ToggleItemChecked(2);
                        _amiMain._outputLabel.Text += "[p][/p][p][code]>AMI: Password protection has been enabled. The application will prompt for a password on startup.[/code][/p]";

                        GD.Print("AmiTextEntry.OnConfirmed case 4: Security Q&A saved successfully");
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiTextEntry.OnConfirmed case 4: Exception: {e.Message}");
                        _textInstructionLabel.Text = "An error occurred. Please try again.";
                        CallTextEntryByUseCase(4); // Re-iterate
                    }
                    break;

                case 5: //Password Entry
                    try
                    {
                        // Load and decipher password from user://Assets/loc/a2d6af3e2115a910.json
                        string encodedPasswordData = Godot.FileAccess.GetFileAsString("user://Assets/loc/a2d6af3e2115a910.json");
                        string passwordData = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encodedPasswordData));
                        string[] parts = passwordData.Split(':');
                        string storedSalt = parts[0];
                        string storedHash = parts[1];

                        // Hash user input with stored salt
                        string userInput = _lineEdit1.Text;
                        string hashedInput = HashWithSalt(userInput, storedSalt);

                        if (hashedInput == storedHash)
                        {
                            // Pass
                            //GD.Print("AmiTextEntry.OnConfirmed case 5: Password correct");
                            _lineEdit1.Clear();
                            _lineEdit1.Secret = false;
                            EmitSignal("PasswordComplete");
                            Visible = false;
                            _retryCount = 0;
                        }
                        else
                        {
                            // Fail
                            //GD.PrintErr("AmiTextEntry.OnConfirmed case 5: Password incorrect");
                            _retryCount++;
                            _lineEdit1.Clear();
                            if (_retryCount > 2)
                            {
                                _retryCount = 0;
                                CallTextEntryByUseCase(6);
                            }
                            else
                            {
                                _textInstructionLabel.Text = $"Incorrect password. {3 - _retryCount} attempts remaining.";
                                Visible = true;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiTextEntry.OnConfirmed case 5: Exception: {e.Message}");
                        CallTextEntryByUseCase(6);
                    }
                    break;

                case 6: // Security Answer Entry
                    try
                    {
                        // Load and display security question
                        string encodedQuestion = FileAccess.GetFileAsString("user://Assets/loc/71200e1b9b2258b4.json");
                        string plainQuestion = Encoding.UTF8.GetString(Convert.FromBase64String(encodedQuestion));
                        GD.Print("Security Question is ", plainQuestion);
                        _textInstructionLabel.Text = plainQuestion + "?";

                        // Load and decipher answer
                        string encodedAnswerData = FileAccess.GetFileAsString("user://Assets/loc/5852283cdccc2eb3.json");
                        string answerData = Encoding.UTF8.GetString(Convert.FromBase64String(encodedAnswerData));
                        string[] aParts = answerData.Split(':');
                        string storedSalt = aParts[0];
                        string storedHash = aParts[1];

                        // Hash user input with stored salt
                        string userInput = _lineEdit1.Text;
                        string hashedInput = HashWithSalt(userInput, storedSalt);

                        if (hashedInput == storedHash)
                        {
                            // Pass
                            //GD.Print("AmiTextEntry.OnConfirmed case 6: Answer correct");
                            _lineEdit1.Clear();
                            EmitSignal("PasswordComplete");
                            Visible = false;
                            _retryCount = 0;
                        }
                        else
                        {
                            // Fail
                            //GD.PrintErr("AmiTextEntry.OnConfirmed case 6: Answer incorrect");
                            _retryCount++;
                            _lineEdit1.Clear();
                            if (_retryCount > 2)
                            {
                                _retryCount = 0;
                                CallTextEntryByUseCase(7);
                            }
                            else
                            {
                                _textInstructionLabel.Text = $"Incorrect answer. {3 - _retryCount} attempts remaining.";
                                Visible = true;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiTextEntry.OnConfirmed case 6: Exception: {e.Message}");
                        CallTextEntryByUseCase(7);
                    }
                    break;

                case 7: // API Key entry for lost password
                    try
                    {
                        string userApiKey = _lineEdit1.Text;
                        if (userApiKey == _appManager._apiKey)
                        {
                            // Pass
                            //GD.Print("AmiTextEntry.OnConfirmed case 7: API key correct");
                            _lineEdit1.Clear();
                            EmitSignal("PasswordComplete");
                            Visible = false;
                            _retryCount = 0;
                        }
                        else
                        {
                            // Fail
                            //GD.PrintErr("AmiTextEntry.OnConfirmed case 7: API key incorrect");
                            _retryCount++;
                            _lineEdit1.Clear();
                            if (_retryCount > 2)
                            {
                                _retryCount = 0;
                                CallTextEntryByUseCase(8);
                            }
                            else
                            {
                                _textInstructionLabel.Text = $"Incorrect API key. {3 - _retryCount} attempts remaining.";
                                Visible = true;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiTextEntry.OnConfirmed case 7: Exception: {e.Message}");
                        CallTextEntryByUseCase(8);
                    }
                    break;

                case 8: // Confirm password disable and API deletion
                    try
                    {
                        // Delete files
                        if (Godot.FileAccess.FileExists("user://Assets/loc/a2d6af3e2115a910.json"))
                            Godot.DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath("user://Assets/loc/a2d6af3e2115a910.json"));
                        if (Godot.FileAccess.FileExists("user://Assets/loc/71200e1b9b2258b4.json"))
                            Godot.DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath("user://Assets/loc/71200e1b9b2258b4.json"));
                        if (Godot.FileAccess.FileExists("user://Assets/loc/5852283cdccc2eb3.json"))
                            Godot.DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath("user://Assets/loc/5852283cdccc2eb3.json"));
                        if (Godot.FileAccess.FileExists("user://Assets/loc/0cc27a50c7cc31eb.json"))
                            Godot.DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath("user://Assets/loc/0cc27a50c7cc31eb.json"));

                        // Clear API key
                        _appManager._apiKey = "";

                        // Toggle menu item
                        _amiMain._preferencesButton.GetPopup().SetItemChecked(2, false);

                        // Clear and hide
                        _lineEdit1.Clear();
                        EmitSignal("PasswordComplete");
                        Visible = false;
                        GD.Print("AmiTextEntry.OnConfirmed case 8: Password disabled, API deleted");
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiTextEntry.OnConfirmed case 8: Exception: {e.Message}");
                    }
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
            if (_useCase > 4 && _useCase < 9)
            {
                //quit application
                GetTree().Quit();
                GD.Print($"TextEntry: Canceled during password entry. Exited program.");
                QueueFree();

                return;
            }

            _lineEdit1.Clear();
            _lineEdit2.Clear();
            _lineEdit1.Secret = false;
            _lineEdit2.Secret = false;
            GD.Print($"TextEntry: Canceled for use case {_useCase}. Cleared inputs.");
        }

        // Utility method to generate a random salt
        private static string GenerateSalt()
        {
            byte[] saltBytes = new byte[16];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }
            return Convert.ToBase64String(saltBytes);
        }

        // Utility method to hash input with salt using SHA-256
        private static string HashWithSalt(string input, string salt)
        {
            string saltedInput = salt + input;
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(saltedInput));
            return Convert.ToBase64String(hashBytes);
        }
    }
}

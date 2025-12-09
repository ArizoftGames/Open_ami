using Godot;
using OpenAI;
using OpenAI.Responses;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using WeCantSpell.Hunspell;
using static Godot.HttpClient;
using static System.Net.Mime.MediaTypeNames;
using static System.Net.WebRequestMethods;

namespace OpenAmi.Scripts
{
    public partial class AmiMain : Control
    {
        public AmiDisplay _amiDisplay;
        public AmiTextEntry _amiTextEntry;
        public AmiThink _amiThink;
        
        private AppManager _appManager;
        public OpenAIClient _responsesClient;
        private SpellCheckHighlighter _spellHighlighter;
        public Dictionary<string, object> _modelsData;
        public Dictionary<string, object> _currentModelData;
        public Dictionary<string,object> _personasData;

        public PopupMenu _resumeSubmenu;
        public PopupMenu _personaSubmenu;
        public PopupMenu _fontSubmenu;

        public HttpRequest _httpRequest;
        public RichTextLabel _outputLabel;
        public Label _inLabel2, _outLabel;
        public ItemList _artifactsList;
        public RichTextLabel _modelLabel;
        public RichTextLabel _tokensLabel;
        private RichTextLabel _amiLabel;
        public ItemList _uploadsList;
        public TextEdit _userInputTextEdit;
        public Label _bLabel;
        public Label _budgetLabel;
        public Label _sLabel;
        public Label _sessionLabel;
        public Label _rLabel;
        public Label _runningLabel;
        public Label _rpLabel;
        public Label _responseLabel;
        public MenuButton _sessionButton;
        public MenuButton _editButton;
        public MenuButton _preferencesButton, _toolsButton;
        public MenuButton _modelButton;
        public MenuButton _informationButton, _helpButton;
        public OptionButton _localizationButton;
        public Button _updateButton, _quitButton;
        public Button _sendButton, _uploadButton; //_micButton;
        
        public int _switchIndex = 0; // 0 = user input, 1 = API key input, 2 = Setup Sequence start
        public string _lastRequestType = "";
        public string _viewFilePath;
        public string _viewFileName;
        public string _viewFileContents;
        public string _modelName;
        public string _filePath = "";
        private string _currentMisspelledWord;
        private Vector2 _mousePos;
        public string _currencyAbbrev = "USD";
        private string _userNameInfo = "User name declined; do not assume identity unless user specifies";
        private string _userAgeInformation = "User age declined; do not assume age unless user specifies";


        public override void _Ready()
        { 
            //Initalize  fields
            _httpRequest = GetNode<HttpRequest>("HTTPRequest");
            _outputLabel = GetNode<RichTextLabel>("BGPanelCont/TopContainer/DisplaysContainer/TextBoxDisplays/OutputLabel");
            _userInputTextEdit = GetNode<TextEdit>("BGPanelCont/TopContainer/DisplaysContainer/TextBoxDisplays/UserInputTextEdit");
            _uploadButton = GetNode<Button>("BGPanelCont/TopContainer/DisplaysContainer/InfoDisplays/UploadButton");
            _sendButton = GetNode<Button>("BGPanelCont/TopContainer/DisplaysContainer/InfoDisplays/IOButtonsCont/SendButton");
            _inLabel2 = GetNode<Label>("BGPanelCont/TopContainer/DisplaysContainer/InfoDisplays/InLabel2");
            _outLabel = GetNode<Label>("BGPanelCont/TopContainer/DisplaysContainer/InfoDisplays/OutLabel");
            _updateButton = GetNode<Button>("BGPanelCont/TopContainer/DisplaysContainer/InfoDisplays/IOButtonsCont/UpdateButton");
            _artifactsList = GetNode<ItemList>("BGPanelCont/TopContainer/DisplaysContainer/InfoDisplays/ArtifactsList");
            _uploadsList = GetNode<ItemList>("BGPanelCont/TopContainer/DisplaysContainer/InfoDisplays/UploadsList");
            _editButton = GetNode<MenuButton>("BGPanelCont/TopContainer/MenuBarContainer/EditButton");
            _bLabel = GetNode<Label>("BGPanelCont/TopContainer/DisplaysContainer/InfoDisplays/SpendInfoCont/BudgetCont/BLabel");
            _budgetLabel = GetNode<Label>("BGPanelCont/TopContainer/DisplaysContainer/InfoDisplays/SpendInfoCont/BudgetCont/BudgetLabel");
            _rpLabel = GetNode<Label>("BGPanelCont/TopContainer/DisplaysContainer/InfoDisplays/SpendInfoCont/ResponseCont/RPLabel");
            _responseLabel = GetNode<Label>("BGPanelCont/TopContainer/DisplaysContainer/InfoDisplays/SpendInfoCont/ResponseCont/ResponseLabel");
            _sLabel = GetNode<Label>("BGPanelCont/TopContainer/DisplaysContainer/InfoDisplays/SpendInfoCont/SessionCont/SLabel");
            _sessionLabel = GetNode<Label>("BGPanelCont/TopContainer/DisplaysContainer/InfoDisplays/SpendInfoCont/SessionCont/SessionLabel");
            _rLabel = GetNode<Label>("BGPanelCont/TopContainer/DisplaysContainer/InfoDisplays/SpendInfoCont/RunningCont/RLabel");
            _runningLabel = GetNode<Label>("BGPanelCont/TopContainer/DisplaysContainer/InfoDisplays/SpendInfoCont/RunningCont/RunningLabel");
            _sessionButton = GetNode<MenuButton>("BGPanelCont/TopContainer/MenuBarContainer/SessionButton");
            _preferencesButton = GetNode<MenuButton>("BGPanelCont/TopContainer/MenuBarContainer/PreferencesButton");
            _toolsButton = GetNode<MenuButton>("BGPanelCont/TopContainer/MenuBarContainer/ToolsButton"); 
            _modelButton = GetNode<MenuButton>("BGPanelCont/TopContainer/MenuBarContainer/ModelButton");
            _informationButton = GetNode<MenuButton>("BGPanelCont/TopContainer/MenuBarContainer/InformationButton");
            _helpButton = GetNode<MenuButton>("BGPanelCont/TopContainer/MenuBarContainer/HelpButton");
            _quitButton = GetNode<Button>("BGPanelCont/TopContainer/MenuBarContainer/QuitButton");
            _localizationButton = GetNode<OptionButton>("BGPanelCont/TopContainer/MenuBarContainer/LocalizationButton");
            _tokensLabel = GetNode<RichTextLabel>("BGPanelCont/TopContainer/FooterContainer/TokensLabel");
            _amiLabel = GetNode<RichTextLabel>("BGPanelCont/TopContainer/FooterContainer/AMILabel");
            _modelLabel = GetNode<RichTextLabel>("BGPanelCont/TopContainer/FooterContainer/ModelLabel");
            
            _appManager = GetNode<AppManager>("/root/AppManager");
            _spellHighlighter = new SpellCheckHighlighter(_appManager, _userInputTextEdit);
            _resumeSubmenu = new PopupMenu { Name = "Resume Session" };
            _fontSubmenu = new PopupMenu { Name = "Font Face" };
            _personaSubmenu = new PopupMenu { Name = "Personas" };
            _currentModelData = [];

            _userInputTextEdit.SyntaxHighlighter = _spellHighlighter;

            _sendButton.Pressed += OnSendButtonPressed;

           

            _modelName = _appManager._config.TryGetValue("ModelSelected", out var model) ? model.ToString() : "grok-4-fast-non-reasoning";
            GD.Print($"Main:_Ready - Model selected: {_modelName}");

            // Instantiate AMI_Display.tscn, AMI_TextEntry and AMI_Think
            if (ResourceLoader.Exists("res://Scenes/AMI_Display.tscn") && ResourceLoader.Exists("res://Scenes/AMI_TextEntry.tscn"))
            {
                var scene = ResourceLoader.Load<PackedScene>("res://Scenes/AMI_Display.tscn");
                _amiDisplay = (AmiDisplay)scene.Instantiate();
                this.AddChild(_amiDisplay);
                _amiDisplay.Visible = false;
                //GD.Print("Main:_Ready - AMI_Display instantiated and added to scene tree");

                scene = ResourceLoader.Load<PackedScene>("res://Scenes/AMI_TextEntry.tscn");
                _amiTextEntry = (AmiTextEntry)scene.Instantiate();
                this.AddChild(_amiTextEntry);
                _amiTextEntry.Visible = false;
                //GD.Print("Main:_Ready - AMI_TextEntry instantiated and added to scene tree");

                scene = ResourceLoader.Load<PackedScene>("res://Scenes/AMI_Think.tscn");
                _amiThink = (AmiThink)scene.Instantiate();
                this.AddChild(_amiThink);
                _amiThink.Visible = true;
                _amiThink._anim.Active = true;
                
                _amiThink._anim.Play("think");
            }
            else
            {
                GD.PrintErr("Main:_Ready - Error: AMI_Display.tscn or AMIFileList.tscn not found.");
            }

            //Initialize OpenAI Client
            var encodedApiKey = Godot.FileAccess.GetFileAsString("user://Context/cached.json");
            _appManager._apiKey = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(encodedApiKey));
            if (!string.IsNullOrEmpty(_appManager._apiKey))
            {
                var options = new OpenAIClientOptions
                {
                    Endpoint = new Uri("https://api.x.ai/v1/")
                };
                
                _responsesClient = new OpenAIClient(new ApiKeyCredential(_appManager._apiKey), options);

                
                //GD.Print("AmiMain: Initialized ResponsesClient");
            }
            else
                GD.PrintErr("AmiMain._Ready: No API key present for ResponsesClient");

            //Set default theme font if present
            try
            {
                if (_appManager._config.TryGetValue("DefaultFont", out var font))
                {
                    this.Theme.DefaultFont = GD.Load<Godot.Font>($"res://Assets/Fonts/{_appManager._config["DefaultFont"]}");
                }
            }
            catch (Exception e)
            {
                GD.PrintErr("AppMain._Ready:  Font set failed: ", e);
            }

            //Initialize AMI UI

            _appManager.InitializeApp(this);

            //Initialize Font Size
            try
            {
                Theme.DefaultFontSize = 36;

                if (_appManager._config.TryGetValue("FontSize", out var fontSize))
                {
                    Theme.DefaultFontSize = fontSize.ToString().ToInt();
                    GD.Print("AmiMain._Ready:  Font size set from config: ", Theme.DefaultFontSize);
                }
                else
                    GD.Print("No font size override found; using default 36 px.");
            }
            catch (Exception e)
            {
                GD.PrintErr("AmiMain._Ready: Font Size set failed: ", e);
            }

            //Initialize override fonts
            try
            {
                string baseName = _appManager._config["DefaultFont"].ToString().Replace(".ttf", "");
                string italicName = $"{baseName}-Italic.ttf";
                var newItalicFont = ResourceLoader.Load<Godot.Font>($"res://Assets/Fonts/{italicName}", "Font");
                GD.Print("Font: Loaded italic resource from ", italicName);
                if (newItalicFont != null)
                {
                    _outputLabel.AddThemeFontOverride("italics_font", newItalicFont);
                    _amiDisplay._displayLabel.AddThemeFontOverride("italics_font", newItalicFont);
                    GD.Print($"New italics: {newItalicFont}, {this.HasThemeFontOverride("italics_font")}");
                }
                else
                {
                    GD.PrintErr($"Italic font not found: {italicName}");
                }

                // Derive and load bold
                string boldName = $"{baseName}-Bold.ttf";
                var newBoldFont = ResourceLoader.Load<Godot.Font>($"res://Assets/Fonts/{boldName}", "Font");
                GD.Print("Font: Loaded bold resource from ", boldName);
                if (newBoldFont != null)
                {
                    _outputLabel.AddThemeFontOverride("bold_font", newBoldFont);
                    _amiDisplay._displayLabel.AddThemeFontOverride("bold_font", newBoldFont);
                    GD.Print($"New bold: {newBoldFont}, {this.HasThemeFontOverride("bold_font")}");
                }
                else
                {
                    GD.PrintErr($"Bold font not found: {boldName}");
                }

                // Derive and load bold-italic
                string boldItalicName = $"{baseName}-BoldItalic.ttf";
                var newBoldItalicFont = ResourceLoader.Load<Godot.Font>($"res://Assets/Fonts/{boldItalicName}", "Font");
                GD.Print("Font: Loaded bold italic resource from ", boldItalicName);
                if (newBoldItalicFont != null)
                {
                    _outputLabel.AddThemeFontOverride("bold_italics_font", newBoldItalicFont);
                    _amiDisplay._displayLabel.AddThemeFontOverride("bold_italics_font", newBoldItalicFont);
                    GD.Print($"New bold italics: {newBoldItalicFont}, {_outputLabel.HasThemeFontOverride("bold_italics_font")}");
                }
                else
                {
                    GD.PrintErr($"Bold-italic font not found: {boldItalicName}");
                }
            }
            catch (Exception e)
            {
                GD.PrintErr("AmiMain._Ready: error initializing FontOverrides: ", e);
            }

            //Signal connections

            _sessionButton.GetPopup().IndexPressed += SessionButtonIndexPressed;
            _editButton.GetPopup().IndexPressed += OnEditButtonIndexPressed;
            _preferencesButton.GetPopup().IndexPressed += OnPreferencesButtonIndexPressed;
            _modelButton.GetPopup().IndexPressed += OnModelButtonIndexPressed;
            _toolsButton.GetPopup().IndexPressed += OnToolsButtonIndexPressed;
            _informationButton.GetPopup().IndexPressed += OnInformationButtonIndexPressed;
            _helpButton.GetPopup().IndexPressed += OnHelpButtonIndexPressed;
            _localizationButton.GetPopup().IndexPressed += OnLocalizationButtonIndexPressed;
            _userInputTextEdit.GetMenu().IdPressed += OnContextMenuOptionPressed;
            _userInputTextEdit.GuiInput += OnTextEditGuiInput;

            _outputLabel.FocusEntered += OnFocusEntered;
            _outputLabel.FocusExited += OnFocusExited;
            _artifactsList.FocusEntered += OnFocusEntered;
            _artifactsList.FocusExited += OnFocusExited;

            // Connect submenu IndexPressed to handlers
           
            _resumeSubmenu.IndexPressed += OnResumeSessionIndexPressed;
            _personaSubmenu.IndexPressed += OnPersonaSubmenuIndexPressed;

            //Set tooltips for local menu buttons
            _toolsButton.TooltipText = "Tools that Grok can\n apply to your query.";
            _toolsButton.GetPopup().SetItemTooltip(0, "Enable or disable all tools");
            _toolsButton.GetPopup().SetItemTooltip(1, "Enable or disable Collection Search Tool");
            _toolsButton.GetPopup().SetItemTooltip(2, "Enable or disable Web Search Tool");
            _toolsButton.GetPopup().SetItemTooltip(3, "Enable or disable Code Execution Tool");

            _informationButton.TooltipText = "Open your xAI console, update your xAI API Key or edit your user information or custom instructions.";
            _informationButton.GetPopup().SetItemTooltip(0, "Open a browser window to your xAI console");
            _informationButton.GetPopup().SetItemTooltip(1, "Input a new API key");
            _informationButton.GetPopup().SetItemTooltip(4, "Manage your Collection");
            _informationButton.GetPopup().SetItemTooltip(5, "Update your CollectionID");

            _helpButton.GetPopup().SetItemTooltip(0, "Open the AMI User Guide");
            _helpButton.GetPopup().SetItemTooltip(1, "Learn more about tokens and usage");
            _helpButton.GetPopup().SetItemTooltip(2, "Open a browser window to view\nxAI's Grok API documentation");
            _helpButton.GetPopup().SetItemTooltip(3, "View AMI application information and credits");
            _helpButton.GetPopup().SetItemTooltip(4, "Learn about Collections");

            GD.Print("Main:_Ready - AMI initialized successfully.");
        }
        public void CloseDisplayWindow()
        {
            if (_amiDisplay != null)
            {
                  _amiDisplay.Visible = false;
                GD.Print("Main: CloseDisplayWindow - Display Window visibility off.");
            }
            else
            {
                GD.PrintErr("Main: CloseDisplayWindow - Error: _amiDisplay is null.");
            }
        }

       // Handles focus gained by OutputLabel, ReasoningLabel, or BookshelfLabel, disabling non-functional EditButton items
        public void OnFocusEntered()
        {
            // Ensure EditButton is initialized to prevent null reference errors
            if (_editButton == null)
            {
                GD.PrintErr("AmiMain:OnFocusEntered - Error: EditButton is null");
                return;
            }

            // Get the focused control to identify which RichTextLabel triggered the signal
            var focusedControl = GetViewport().GuiGetFocusOwner();
            if (focusedControl != _outputLabel && focusedControl != _artifactsList)
            {
                GD.PrintErr($"AmiMain:OnFocusEntered - Error: Invalid focused control type {focusedControl?.GetType().Name}");
                return;
            }

            // Disable Cut (0), Paste (2), and Delete (3) in EditButton popup as they are not supported for RichTextLabels
            var popup = _editButton.GetPopup();
            foreach (var index in new[] { 0, 2, 3 })
            {
                popup.SetItemDisabled(index, true);
            }

            // Log the action with the name of the focused node
            //GD.Print($"AmiMain:OnFocusEntered - Disabled Cut/Paste/Delete for {focusedControl.Name}");
        }

        // Handles focus lost by OutputLabel, ReasoningLabel, or BookshelfLabel, re-enabling all EditButton items
        public void OnFocusExited()
        {
            // Ensure EditButton is initialized to prevent null reference errors
            if (_editButton == null)
            {
                GD.PrintErr("AmiMain:OnFocusExited - Error: EditButton is null");
                return;
            }

            // Get the focused control to log which node lost focus
            var focusedControl = GetViewport().GuiGetFocusOwner();
            if (focusedControl != null && focusedControl != _outputLabel && focusedControl != _artifactsList)
            {
                GD.PrintErr($"AmiMain:OnFocusExited - Error: Invalid focused control type {focusedControl?.GetType().Name}");
                return;
            }

            // Re-enable all EditButton items (Cut: 0, Copy: 1, Paste: 2, Delete: 3, Select All: 4)
            var popup = _editButton.GetPopup();
            for (int index = 0; index < 5; index++)
            {
                popup.SetItemDisabled(index, false);
            }

            // Log the action with the name of the node that lost focus (or null if no focus)
            //GD.Print($"AmiMain:OnFocusExited - Re-enabled all EditButton items for {(focusedControl?.Name ?? "none")}");
        }

        private static void OnOutputLabelMetaClicked(Variant meta)
        {
            try
            {
                if ((string)meta != null)
                    OS.ShellOpen((string)meta);
                else
                    GD.PrintErr("OnOutputLabelMetaClicked URL opening failed: meta invalid");
            }
            catch (Exception e)
            {
                GD.PrintErr("OnOutputLabelMetaClicked: URL opening failed: ", e);
            }
        }

        // Handles SessionButton menu item selections for session-related operations
        public async void SessionButtonIndexPressed(long index)
        {
            // Ensure AmiDisplay is initialized to prevent null reference errors
            if (_amiDisplay == null)
            {
                GD.PrintErr("AmiMain:SessionButtonIndexPressed - Error: AmiDisplay is null");
                return;
            }

            // Handle operations based on menu item index
            switch (index)
            {
                case 0: // New Session

                    // Save session legacy data
                    if (!Godot.FileAccess.FileExists("user://Context/SessionLegacy.txt"))
                    {
                        using var legacyfile = Godot.FileAccess.Open("user://Context/SessionLegacy.txt", Godot.FileAccess.ModeFlags.Write);
                        if (legacyfile != null)
                        {
                            legacyfile.StoreString("Session Legacy Log\n");
                            //GD.Print("AmiMain:Newsession - Created SessionLegacy.txt");
                        }
                        else
                        {
                            GD.PrintErr("AmiMain:NewSession - Error: FileAccess null when creating SessionLegacy.txt");
                        }
                    }

                    //Start thinking animation
                    if (_amiThink != null)
                    {
                        _amiThink.Visible = true;
                        _amiThink._anim.Active = true;
                        _amiThink._anim.Play("think");
                        //GD.Print("AmiMain.NewSession: AmiThink visible = ,", _amiThink.Visible,  " at ", DateTime.Now.ToString("hhmm:ss.fff"));
                    }
                    else
                        GD.PrintErr("AMiMain.NewSession: Think Animation failed - AmiThink not found");

                    var sessionLegacyPath = "user://Context/SessionLegacy.txt";
                    string sessionID = _appManager._config.TryGetValue("SessionID", out var id) ? id.ToString() : "none";
                    string lastResponseID = _appManager._config.TryGetValue("LastResponseID", out var responseId) ? responseId.ToString() : "none";
                    string timestamp = DateTime.Now.ToString("HH.mm MM/dd/yy");
                    var sessionEntry = $"SessionID: {sessionID}, LastResponseID: {lastResponseID}, Last Access: {timestamp}, Spend: {_appManager._config["SessionSpend"]}\n";
                    Godot.FileAccess file = Godot.FileAccess.Open(sessionLegacyPath, Godot.FileAccess.ModeFlags.ReadWrite);
                    if (file != null)
                    {
                        file.SeekEnd();
                        file.StoreString(sessionEntry);
                        //GD.Print("NewSession: - Appended session data to SessionLegacy.txt");
                    }
                    else
                    {
                        GD.PrintErr($"NewSession: - Error: FileAccess null for {sessionLegacyPath}");
                    }

                    try
                    {
                        // Clear previous session data
                        _appManager.ClearPreviousSession();

                        // Clear UploadsList and ArtifactsList
                        if (_uploadsList != null || _artifactsList != null)
                        {
                            _uploadsList.Clear();
                            _artifactsList.Clear();
                        }
                        else
                        {
                            GD.PrintErr("AmiMain:SessionButtonIndexPressed - Error: UploadsList or ArtifactsList is null");
                        }

                        //Clear Session Spend
                        _appManager._config["SessionSpend"] = "00.0000";
                        
                        // Update UI
                        if (_outputLabel != null)
                        {
                            _outputLabel.Text += "[p]New session started.[/p]";
                        }
                        
                        GD.Print("AmiMain:SessionButtonIndexPressed - New session started");
                        
                        //Assign new SessionID
                        _appManager._config["SessionID"] = DateTime.Now.ToString("MMdd.HHmm");
                        //GD.Print("New session ID assigned: ",_appManager._config["SessionID"].ToString());

                            // Send new session prompt
                            if (_responsesClient != null)
                        {
                            _lastRequestType = "query_usage";
                            var (messages, responseOptions) = _appManager.WritePrompt("");
                            OpenAIResponseClient localClient = _responsesClient.GetOpenAIResponseClient(_appManager._config["ModelSelected"].ToString());
                            responseOptions.PreviousResponseId = null;// Ensure no previous response ID for new session

                            //Write system prompt
                            string rolePlayText = "";
                            try
                            {

                            if (_personasData.TryGetValue("Personas", out var dataObj) && dataObj is JsonElement dataElement && dataElement.ValueKind == JsonValueKind.Array)
                                foreach (var persona in dataElement.EnumerateArray())
                            {
                                if (persona.GetProperty("name").ToString() == _appManager._config["PersonaSelected"].ToString())
                                {
                                    rolePlayText= persona.GetProperty("system").ToString();
                                }
                            }
                            }
                            catch (Exception e)
                            {
                                GD.PrintErr("AmiMain:NewSession- Error reading persona data: ", e);
                            }
                           string userInfoText = Godot.FileAccess.GetFileAsString("user://Assets/user.txt");
                            string systemInstructions = Godot.FileAccess.GetFileAsString("res://Assets/Instructions.txt");
                            string customInstructions = Godot.FileAccess.GetFileAsString("user://Assets/custom.txt");
                            string systemText = $"{rolePlayText}\n\n{userInfoText}\n\n{systemInstructions}\n\n{customInstructions}";
                            GD.Print("NewSession: systemText");
                            responseOptions.Instructions = systemText; //Set role : system prompt

                            GD.Print("AmiMain:NewSession- new session prompt to API: ", messages, ", ", responseOptions.PreviousResponseId);
                            var result = await localClient.CreateResponseAsync(messages, responseOptions);
                            if (result != null)
                                _appManager.ParseResponsesAPIResponse(result.Value, true);
                            else
                                GD.Print("AmiMain:NewSession - No response received from CreateResponseAsync");
                        }
                        else
                        {
                            GD.PrintErr("AmiMain:NewSession- Error: ResponsesClient is null");
                        }


                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiMain:SessionButtonIndexPressed - Error starting new session: {e.Message}");
                        _outputLabel.Text += $"[p]Error starting new session: {e.Message}[/p]";
                    }
                    break;

                case 1: // Resume Session
                    break;

                case 2: // Session History
                    try
                    {
                        // Read session_history.json from user://Context/
                        var sessionHistoryPath = "user://Context/session_history.json";
                        if (Godot.FileAccess.FileExists(sessionHistoryPath))
                        {
                            var json = Godot.FileAccess.GetFileAsString(sessionHistoryPath);
                            _amiDisplay.Visible = true;
                            _amiDisplay.Title = "Viewing Session History";
                            _amiDisplay._displayLabel.Text = json;
                            GD.Print("AmiMain:SessionButtonIndexPressed - Session History pressed. Display Window visibility on.");
                        }
                        else
                        {
                            _amiDisplay.Visible = true;
                            _amiDisplay.Title = "Viewing Session History";
                            _amiDisplay._displayLabel.Text = "No session history available.";
                            GD.PrintErr("AmiMain:SessionButtonIndexPressed - Error: session_history.json not found");
                        }
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiMain:SessionButtonIndexPressed - Error reading session_history.json: {e.Message}");
                        _amiDisplay.Visible = true;
                        _amiDisplay.Title = "Viewing Session History";
                        _amiDisplay._displayLabel.Text = "Error loading session history.";
                    }
                    break;

                case 3: // Query History
                    try
                    {
                        // Read query_history.json from user://Context/
                        var queryHistoryPath = "user://Context/query_history.json";
                        if (Godot.FileAccess.FileExists(queryHistoryPath))
                        {
                            var json = Godot.FileAccess.GetFileAsString(queryHistoryPath);
                            _amiDisplay.Visible = true;
                            _amiDisplay.Title = "Viewing Query History";
                            _amiDisplay._displayLabel.Text = json;
                            GD.Print("AmiMain:SessionButtonIndexPressed - Query History pressed. Display Window visibility on.");
                        }
                        else
                        {
                            _amiDisplay.Visible = true;
                            _amiDisplay.Title = "Viewing Query History";
                            _amiDisplay._displayLabel.Text = "No query history available.";
                            GD.PrintErr("AmiMain:SessionButtonIndexPressed - Error: query_history.json not found");
                        }
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiMain:SessionButtonIndexPressed - Error reading query_history.json: {e.Message}");
                        _amiDisplay.Visible = true;
                        _amiDisplay.Title = "Viewing Query History";
                        _amiDisplay._displayLabel.Text = "Error loading query history.";
                    }
                    break;

                case 4: // Show Last Prompt
                    try
                    {
                        // Use AppManager._lastPrompt directly
                        if (!string.IsNullOrEmpty(_appManager._lastPrompt))
                        {
                            _amiDisplay.Visible = true;
                            _amiDisplay.Title = "Viewing Last Prompt";
                            _amiDisplay._displayLabel.Text = _appManager._lastPrompt;
                            GD.Print("AmiMain:SessionButtonIndexPressed - Show Last Prompt pressed. Display Window visibility on.");
                        }
                        else
                        {
                            _amiDisplay.Visible = true;
                            _amiDisplay.Title = "Viewing Last Prompt";
                            _amiDisplay._displayLabel.Text = "No last prompt available.";
                            GD.PrintErr("AmiMain:SessionButtonIndexPressed - Error: Last prompt is empty");
                        }
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiMain:SessionButtonIndexPressed - Error displaying last prompt: {e.Message}");
                        _amiDisplay.Visible = true;
                        _amiDisplay.Title = "Viewing Last Prompt";
                        _amiDisplay._displayLabel.Text = "Error loading last prompt.";
                    }
                    break;

                 default:
                    GD.PrintErr($"AmiMain:SessionButtonIndexPressed - Error: Invalid SessionButton index {index}");
                    break;
            }
        }

        //Handles index selections from Session > Resume Session
        private async void OnResumeSessionIndexPressed(long index)
        {
            string _spend;                      
            //Start thinking animation
            if (_amiThink != null)
            {
                _amiThink.Visible = true;
                _amiThink._anim.Active = true;
                _amiThink._anim.Play("think");

                //GD.Print("AmiMain.OnResumeSessionIndexPressed: AmiThink visible = ,", _amiThink.Visible,  " at ", DateTime.Now.ToString("hhmm:ss.fff"));
            }
            else
                GD.PrintErr("AMiMain.OnResumeSessionIndexPressed: Think Animation failed - AmiThink not found");
            try
            {
                var resumeList = _appManager._resumeSessions;
                // Validate index and resume data
                if (resumeList == null || index < 0 || index >= resumeList.Count)
                {
                    GD.PrintErr($"AmiMain:OnResumeSessionIndexPressed - Error: Invalid index {index} or no resume data");
                    return;
                }
                else
                   GD.Print($"AmiMain:OnResumeSessionIndexPressed - Resuming session at index {index}");

                    var selectedSession = resumeList[(int)index];
                if (!selectedSession.TryGetValue("SessionID", out var sessionId) || !selectedSession.TryGetValue("LastResponseID", out var lastResponseId))
                {
                    GD.PrintErr("AmiMain:OnResumeSessionIndexPressed - Error: Missing SessionID or LastResponseID in selected session");
                    return;
                }
                else
                { 
                    _spend = selectedSession["Spend"]; 
                }

                // Save current session data to SessionLegacy.txt (replicate New Session save logic)
                if (!Godot.FileAccess.FileExists("user://Context/SessionLegacy.txt"))
                {
                    using var legacyfile = Godot.FileAccess.Open("user://Context/SessionLegacy.txt", Godot.FileAccess.ModeFlags.Write);
                    if (legacyfile != null)
                    {
                        legacyfile.StoreString("Session Legacy Log\n");
                        GD.Print("AmiMain:OnResumeSessionIndexPressed - Created SessionLegacy.txt");
                    }
                    else
                    {
                        GD.PrintErr("AmiMain:OnResumeSessionIndexPressed - Error: FileAccess null when creating SessionLegacy.txt");
                        return;
                    }
                }

                var sessionLegacyPath = "user://Context/SessionLegacy.txt";
                string sessionID = _appManager._config.TryGetValue("SessionID", out var id) ? id.ToString() : "none";
                string lastResponseID = _appManager._config.TryGetValue("LastResponseID", out var responseId) ? responseId.ToString() : "none";
                string timestamp = DateTime.Now.ToString("HH.mm MM/dd/yy");
                var sessionEntry = $"SessionID: {sessionID}, LastResponseID: {lastResponseID}, Last Access: {timestamp},  Spend: {_appManager._config["SessionSpend"]}\n";
                GD.Print("OnResumeSessionIndexPressed: Saving current session to SessionLegacy.txt before resuming another session.\nsessionEntry = ", sessionEntry);
                Godot.FileAccess file = Godot.FileAccess.Open(sessionLegacyPath, Godot.FileAccess.ModeFlags.ReadWrite);
                if (file != null)
                {
                    file.SeekEnd();
                    file.StoreString(sessionEntry);
                    //GD.Print("OnResumeSessionIndexPressed: - Appended session data to SessionLegacy.txt");
                }
                else
                {
                    GD.PrintErr($"OnResumeSessionIndexPressed: - Error: FileAccess null for {sessionLegacyPath}");
                }

                // Clear previous session (replicate New Session clear logic, but keep LastResponseID for resume)
                _appManager.ClearPreviousSession();

                // Clear UploadsList and ArtifactsList
                if (_uploadsList != null || _artifactsList != null)
                {
                    _uploadsList.Clear();
                    _artifactsList.Clear();
                }
                else
                {
                    GD.PrintErr("AmiMain:OnResumeSessionIndexPressed - Error: UploadsList or ArtifactsList is null");
                }

                // Set config to selected session (do NOT assign new SessionID)
                _appManager._config["SessionID"] = sessionId;
                _appManager._config["LastResponseID"] = lastResponseId.Trim();
                _appManager._config["SessionSpend"] = _spend;
                _appManager.SaveConfig();
                GD.Print($"AmiMain:OnResumeSessionIndexPressed - Resumed session {sessionId} with LastResponseID {lastResponseId}");

                // Send summary query
                _lastRequestType = "user_query";
                string userText = $"Resuming session {sessionId} from last response ID {lastResponseId}. Summarize the session to date.";
                var (messages, responseOptions) = _appManager.WritePrompt(userText);
                //GD.Print("AmiMain:OnSessionButtonIndexPressed sending ", messages);                       
                OpenAIResponseClient localClient = _responsesClient.GetOpenAIResponseClient(_appManager._config["ModelSelected"].ToString());
                var result = await localClient.CreateResponseAsync(messages, responseOptions);
                if (result != null)
                {
                    _appManager.ParseResponsesAPIResponse(result.Value, true);
                }
                else
                {
                    GD.PrintErr("AmiMain:OnResumeSessionIndexPressed - No response received from CreateResponseAsync");
                    _outputLabel.Text += "Error resuming session: No response received.";
                }

                try
                {
                    _resumeSubmenu.Clear();
                    _appManager.UpdateSessionButton();
                }
                catch (Exception e)
                {
                    GD.PrintErr($"AmiMain:OnResumeSessionIndexPressed - Error updating SessionButton: {e.Message}");
                }
                //GD.Print("AmiMain:OnResumeSessionIndexPressed - Session resumed successfully.");
            }
            catch (Exception e)
            {
                GD.PrintErr($"AmiMain:OnResumeSessionIndexPressed - Error: {e.Message}");
                _outputLabel.Text += $"Error resuming session: {e.Message}";
            }
        }

        // Handles EditButton menu item selections for text editing operations
        public void OnEditButtonIndexPressed(long index)
        {
            // Get the currently focused control to determine the target of the operation
            var focusedControl = GetViewport().GuiGetFocusOwner();
            if (focusedControl == null)
            {
                GD.PrintErr("AmiMain:OnEditButtonItemPressed - Error: No focused control");
                return;
            }

            // Validate the control is either TextEdit or RichTextLabel
            if (focusedControl != _userInputTextEdit && focusedControl != _outputLabel && focusedControl != _artifactsList)
            {
                GD.PrintErr($"AmiMain:OnEditButtonItemPressed - Error: Invalid control type {focusedControl.GetType().Name}");
                return;
            }

            // Handle operations based on menu item index
            switch (index)
            {
                case 0: // Cut
                    if (focusedControl == _userInputTextEdit)
                    {
                        var textEdit = (TextEdit)focusedControl;
                        if (textEdit.HasSelection())
                        {
                            textEdit.Cut();
                            //GD.Print("AmiMain:OnEditButtonItemPressed - Cut executed on UserInputTextEdit");
                        }
                        else
                        {
                            GD.PrintErr("AmiMain:OnEditButtonItemPressed - Error: No text selected in UserInputTextEdit");
                        }
                    }
                    else
                    {
                        GD.PrintErr($"AmiMain:OnEditButtonItemPressed - Error: Cut not supported for {focusedControl.Name}");
                    }
                    break;

                case 1: // Copy
                    if (focusedControl == _userInputTextEdit)
                    {
                        var textEdit = (TextEdit)focusedControl;
                        if (textEdit.HasSelection())
                        {
                            textEdit.Copy();
                            //GD.Print("AmiMain:OnEditButtonItemPressed - Copied text from UserInputTextEdit");
                        }
                        else
                        {
                            GD.PrintErr("AmiMain:OnEditButtonItemPressed - Error: No text selected in UserInputTextEdit");
                        }
                    }
                    else
                    {
                        var richTextLabel = (RichTextLabel)focusedControl;
                        var selectedText = richTextLabel.GetSelectedText();
                        if (!string.IsNullOrEmpty(selectedText))
                        {
                            try
                            {
                                DisplayServer.ClipboardSet(selectedText);
                                //GD.Print($"AmiMain:OnEditButtonItemPressed - Copied text from {focusedControl.Name}");
                            }
                            catch (Exception e)
                            {
                                GD.PrintErr($"AmiMain:OnEditButtonItemPressed - Error: Clipboard access failed: {e.Message}");
                            }
                        }
                        else
                        {
                            GD.PrintErr($"AmiMain:OnEditButtonItemPressed - Error: No text selected in {focusedControl.Name}");
                        }
                    }
                    break;

                case 2: // Paste
                    if (focusedControl == _userInputTextEdit)
                    {
                        var textEdit = (TextEdit)focusedControl;
                        textEdit.Paste();
                        //GD.Print("AmiMain:OnEditButtonItemPressed - Pasted text to UserInputTextEdit");
                    }
                    else
                    {
                        GD.PrintErr($"AmiMain:OnEditButtonItemPressed - Error: Paste not supported for {focusedControl.Name}");
                    }
                    break;

                case 3: // Delete
                    if (focusedControl == _userInputTextEdit)
                    {
                        var textEdit = (TextEdit)focusedControl;
                        if (textEdit.HasSelection())
                        {
                            textEdit.DeleteSelection();
                            //GD.Print("AmiMain:OnEditButtonItemPressed - Deleted text from UserInputTextEdit");
                        }
                        else
                        {
                            GD.PrintErr("AmiMain:OnEditButtonItemPressed - Error: No text selected in UserInputTextEdit");
                        }
                    }
                    else
                    {
                        GD.PrintErr($"AmiMain:OnEditButtonItemPressed - Error: Delete not supported for {focusedControl.Name}");
                    }
                    break;

                case 4: // Select All
                    if (focusedControl == _userInputTextEdit)
                    {
                        var textEdit = (TextEdit)focusedControl;
                        textEdit.SelectAll();
                        //GD.Print("AmiMain:OnEditButtonItemPressed - Selected all text in UserInputTextEdit");
                    }
                    else
                    {

                        var richTextLabel = (RichTextLabel)focusedControl;
                        try
                        {
                            richTextLabel.SelectAll();
                        }
                        catch (Exception e)
                        {
                            GD.PrintErr($"AmiMain:OnEditButtonItemPressed - Error: Select All failed for {focusedControl.Name}: {e.Message}");
                            return;
                        }
                        if (richTextLabel.SelectionEnabled)
                        {
                            //GD.Print($"AmiMain:OnEditButtonItemPressed - Select All not fully supported for {focusedControl.Name}; selection enabled");
                        }
                        else
                        {
                            GD.PrintErr($"AmiMain:OnEditButtonItemPressed - Error: Select All not supported for {focusedControl.Name}");
                        }
                    }
                    break;

                default:
                    GD.PrintErr($"AmiMain:OnEditButtonItemPressed - Error: Invalid EditButton index {index}");
                    break;
            }
        }

        //Handles Preferences selections
        private void OnPreferencesButtonIndexPressed(long index)
        {
            try
            {
                switch (index)
                {
                    case 0://Implement Persona selection; handled by submenu click handler
                    break;

                    case 1://Implement "Dark Theme" selection

                        //GD.Print("ToggleDarkTheme: Selected option is ", _preferencesButton.GetPopup().GetItemText((int)index));

                        try
                        {
                            //GD.Print("Start Theme = ", _appManager._config["ThemeSelected"].ToString());
                            if (_appManager._config.TryGetValue("ThemeSelected", out var theme) && theme.ToString() == "Light")
                            {
                                //replace "_light" with "_dark" in amiText and tokensText using regex to parse
                                try
                                {
                                    string amiText = _amiLabel.Text;
                                    string tokensText = _tokensLabel.Text;
                                    amiText = System.Text.RegularExpressions.Regex.Replace(amiText, "_light", "_dark");
                                    tokensText = System.Text.RegularExpressions.Regex.Replace(tokensText, "_light", "_dark");
                                    _amiLabel.Text = amiText;
                                    _tokensLabel.Text = tokensText;
                                }
                                catch (Exception e)
                                {
                                    GD.PrintErr("OnPreferencesButtonIndexPressed case 1 Theme Select - Text update failed: ", e);
                                }

                                //update app state to apply dark theme
                                try
                                {
                                    this.Theme = (Theme)GD.Load("res://Resources/AMI_Theme.tres");
                                    _preferencesButton.GetPopup().ToggleItemChecked((int)index);
                                    _appManager._config["ThemeSelected"] = "Dark";
                                    _appManager.SetTheme(true);
                                    _appManager.SaveConfig();
                                }
                                catch (Exception e)
                                {
                                    GD.PrintErr("OnPreferencesButtonIndexPressed case 1 Theme Select - Theme application failed: ", e);

                                }
                            }
                            else if (_appManager._config.TryGetValue("ThemeSelected", out theme) && theme.ToString() == "Dark")
                            {
                                //replace "_dark" with "_light" in amiText and tokensText using regex to parse
                                try
                                {
                                    string amiText = _amiLabel.Text;
                                    string tokensText = _tokensLabel.Text;
                                    amiText = System.Text.RegularExpressions.Regex.Replace(amiText, "_dark", "_light");
                                    tokensText = System.Text.RegularExpressions.Regex.Replace(tokensText, "_dark", "_light");
                                    _amiLabel.Text = amiText;
                                    _tokensLabel.Text = tokensText;
                                }
                                catch (Exception e)
                                {
                                    GD.PrintErr("OnPreferencesButtonIndexPressed case 1 Theme Select - Text update failed: ", e);
                                }

                                //update app state to apply light theme
                                try
                                {
                                    this.Theme = (Theme)GD.Load("res://Resources/AMI-L_Theme.tres");
                                    _preferencesButton.GetPopup().ToggleItemChecked((int)index);
                                    _appManager._config["ThemeSelected"] = "Light";
                                    _appManager.SetTheme(false);
                                    _appManager.SaveConfig();
                                }
                                catch (Exception e)
                                {
                                    GD.PrintErr("OnPreferencesButtonIndexPressed case 1 Theme Select - Theme application failed: ", e);

                                }
                            }
                            else
                                GD.PrintErr("OnPreferencesButtonIndexPressed: Config[ThemeSelected] missing or invalid.");

                        }
                        catch (Exception e)
                        {
                            GD.PrintErr("OnPreferencesButtonIndexPressed case 1 Theme Select failed ", e);
                        }
                   
                    break;

                    case 2:
                        
                            GD.Print("OnPreferencesButtonIndexPressed case 2 pressed");
                        
                        break;


                    case 3://Implement "Spell Check" selection

                        try
                        {
                            //If spellcheck is enabled, disable it and update dependencies

                            if (_appManager._config.TryGetValue("EnableSpellcheck", out var enabled) && enabled is JsonElement jsonElmEnabled && jsonElmEnabled.GetBoolean())
                            {
                                //GD.Print("Prefs case 3: JsonElmEnabled is ", jsonElmEnabled.GetBoolean()); 
                                _preferencesButton.GetPopup().ToggleItemChecked(3);
                                 _appManager._config["EnableSpellcheck"] = false;
                                _appManager.SaveConfig();
                                _outputLabel.Text += $"\n\n[code]AMI> Spell Check disabled;  will take effect after restart.[/code]";
                                //GD.Print("Prefs.case 3; spellcheck disabled, EnableSpellCheck = ", _appManager._config["EnableSpellcheck"]);
                            }
                            //Or, if spellcheck is disabled, update dependencies and load dictionaries
                            else if (_appManager._config.TryGetValue("EnableSpellcheck", out enabled) && enabled is JsonElement jsonElmEnabled2 && !jsonElmEnabled2.GetBoolean())
                            {
                               //GD.Print("Prefs case 3: entered false case");
                                _preferencesButton.GetPopup().ToggleItemChecked(3);
                                _appManager._config["EnableSpellcheck"] = true;
                                _appManager.SaveConfig();
                                _appManager.LoadSpellChecker();
                                _outputLabel.Text += $"\n\n[code]AMI> Spell Check enabled;  will take effect after restart.[/code]";
                                //GD.Print("Prefs.case 3; spellcheck disabled, EnableSpellCheck = ", _appManager._config["EnableSpellcheck"]);
                            }
                        }
                        catch (Exception e)
                        {
                            GD.PrintErr("OnPreferencesButtonIndexPressed case 3 Spell Check failed: ", e);
                        }

                        break;

                    case 4://Implement "Font" selection; handled in submenu click handler
                    break;

                    case 5://Implement "Font Size" selection

                        try
                        {
                            _amiTextEntry.CallTextEntryByUseCase(2);
                            //GD.Print($"Prefs:Fontsize succeeded; I/O size {_appManager._config["FontSize"]}");

                        }
                        catch (Exception e)
                        {
                            GD.PrintErr("Prefs:FontSize failed: ", e);
                        }                

                    break;

                    case 6://Implement "Voice" selection
                        try
                        {
                            if (_appManager._config.TryGetValue("EnableVoice", out var voice) && voice.ToString() == "true")
                            {
                                _appManager._config["EnableVoice"] = "false";
                                _preferencesButton.GetPopup().SetItemChecked((int)index, false);
                                GD.Print("Prefs:Voice disabled in config");
                                _outputLabel.Text += $"\n\n[code]AMI> Voice feature off. [/code]";
                                GD.Print("Prefs:Voice disabled, mic button hidden");
                            }
                            else if (_appManager._config.TryGetValue("EnableVoice", out voice) && voice.ToString() == "false")
                            {
                                _appManager._config["EnableVoice"] = "true";
                                _preferencesButton.GetPopup().SetItemChecked((int)index, true);
                                _appManager.SaveConfig();
                                GD.Print("Prefs:Voice enabled in config");
                                _outputLabel.Text += $"\n\n[code]AMI> Voice feature on.[/code]";
                                if (!Directory.Exists(VoiceManager.Singleton._piperDir))
                                {
                                    _outputLabel.AppendText("\n\nBuilding TTS Engine");
                                    Directory.CreateDirectory(VoiceManager.Singleton._piperDir);
                                    VoiceManager.Singleton.CopyPiperRecursively("res://External/piper131", VoiceManager.Singleton._piperDir);
                                    GD.Print("VoiceManager: Piper extraction to user:// complete");
                                    _outputLabel.AppendText("\n\nDone.");
                                }
                                else
                                {
                                    GD.Print("VoiceManager: Piper already in user://, skipping extraction");
                                }
                                GD.Print("Prefs:Voice enabled, mic button visible");
                            }

                        }
                        catch (Exception e)
                        {
                            GD.PrintErr("OnPreferencesButtonIndexPressed case 6 Voice selection failed: ", e);
                        }

                        break;

                    default:
                        GD.PrintErr("OnPreferencesButtonIndexPressed unrecognized index");
                    break;

                }
            }
            catch(Exception e)
            {
                GD.PrintErr("OnPreferencesButtonIndexPressed failed:  Error ", e);
            }
        }

        public async void OnPersonaSubmenuIndexPressed(long index)
        {
            //GD.Print("Persona selection pressed, index = ", index);
            try
            {
                var personaSubmenu = _preferencesButton.GetPopup().GetItemSubmenuNode(0);
                var personaSelected = personaSubmenu.GetItemText((int)index);
                //GD.Print("PersonaSelected = ", personaSelected);
                if (personaSelected != null)
                {
                    _appManager._config["PersonaSelected"] = personaSelected;
                    _appManager.SaveConfig();
                }
                else
                    GD.PrintErr("PersonaSelected not found");
                for (int i = 0; i < personaSubmenu.ItemCount; i++)
                    personaSubmenu.SetItemChecked(i, false);
                personaSubmenu.ToggleItemChecked((int)index);
            }
            catch (Exception e)
            {
                GD.PrintErr("Persona config application failed: ", e);
            }
            //call Writeprompt for persona value, then create response
            if (_personasData != null && _personasData.TryGetValue("Personas", out var dataObj) && dataObj is JsonElement dataElement && dataElement.ValueKind == JsonValueKind.Array)
                foreach (var persona in dataElement.EnumerateArray())
            {
                //GD.Print("Checking persona key: ", persona.GetProperty("name").ToString(), " against selected: ", _appManager._config["PersonaSelected"].ToString());
                if (persona.TryGetProperty("name", out var nameElement) && nameElement.GetString() == _appManager._config["PersonaSelected"].ToString())
                { 
                    //GD.Print("AmiMain:OnPersonaSubmenuIndexPressed - Persona selected: ", persona.GetProperty("system").ToString());
                    _amiThink.Visible = true;
                    _amiThink._anim.Active = true;
                    _amiThink._anim.Play("think");

                    _lastRequestType = "user_query";
                    var (messages, responseOptions) = _appManager.WritePrompt($"Roleplay has been reset to {persona.GetProperty("system")}.  Please respond in that persona to all subsequent queries and acknowledge the change.");
                    OpenAIResponseClient localClient = _responsesClient.GetOpenAIResponseClient(_appManager._config["ModelSelected"].ToString());
                    var result = await localClient.CreateResponseAsync(messages, responseOptions);
                    if (result != null)
                        _appManager.ParseResponsesAPIResponse(result.Value, true);
                    else
                        GD.PrintErr("AmiMain:OnSendButtonPressed - No response received from CreateResponseAsync");
                }
                //else
                    //GD.Print("Persona key ", persona.GetProperty("name").ToString(), " does not match selected persona ", _appManager._config["PersonaSelected"].ToString());
            }
        }

        public void OnFontSubmenuIndexPressed(long index)
        {
            try
            {
                var fontSubmenu = _preferencesButton.GetPopup().GetItemSubmenuNode(4);
                var fontSelected = fontSubmenu.GetItemText((int)index);
                GD.Print("FontSelected = ", fontSelected);

                if (fontSelected != null)
                {
                    var newFont = ResourceLoader.Load<Godot.Font>($"res://Assets/Fonts/{fontSelected}", "Font");
                    GD.Print("Font: Loaded font resource from ", fontSelected);
                    this.Theme.DefaultFont = newFont;
                    GD.Print("Font: Applied new font to Theme.DefaultFont: ", this.Theme.DefaultFont.ToString(), ", ", this.Theme.HasDefaultFont());

                    // Derive and load italic
                    string baseName = fontSelected.Replace(".ttf", "");
                    string italicName = $"{baseName}-Italic.ttf";
                    var newItalicFont = ResourceLoader.Load<Godot.Font>($"res://Assets/Fonts/{italicName}", "Font");
                    GD.Print("Font: Loaded italic resource from ", italicName);
                    if (newItalicFont != null)
                    {
                        _outputLabel.AddThemeFontOverride("italics_font", newItalicFont);
                        _amiDisplay._displayLabel.AddThemeFontOverride("italics_font", newItalicFont);
                        GD.Print($"New italics: {newItalicFont}, {this.HasThemeFontOverride("italics_font")}");
                    }
                    else
                    {
                        GD.PrintErr($"Italic font not found: {italicName}");
                    }

                    // Derive and load bold
                    string boldName = $"{baseName}-Bold.ttf";
                    var newBoldFont = ResourceLoader.Load<Godot.Font>($"res://Assets/Fonts/{boldName}", "Font");
                    GD.Print("Font: Loaded bold resource from ", boldName);
                    if (newBoldFont != null)
                    {
                        _outputLabel.AddThemeFontOverride("bold_font", newBoldFont);
                        _amiDisplay._displayLabel.AddThemeFontOverride("bold_font", newBoldFont);
                        GD.Print($"New bold: {newBoldFont}, {this.HasThemeFontOverride("bold_font")}");
                    }
                    else
                    {
                        GD.PrintErr($"Bold font not found: {boldName}");
                    }

                    // Derive and load bold-italic
                    string boldItalicName = $"{baseName}-BoldItalic.ttf";
                    var newBoldItalicFont = ResourceLoader.Load<Godot.Font>($"res://Assets/Fonts/{boldItalicName}", "Font");
                    GD.Print("Font: Loaded bold italic resource from ", boldItalicName);
                    if (newBoldItalicFont != null)
                    {
                        _outputLabel.AddThemeFontOverride("bold_italics_font", newBoldItalicFont);
                        _amiDisplay._displayLabel.AddThemeFontOverride("bold_italics_font", newBoldItalicFont);
                        GD.Print($"New bold italics: {newBoldItalicFont}, {_outputLabel.HasThemeFontOverride("bold_italics_font")}");
                    }
                    else
                    {
                        GD.PrintErr($"Bold-italic font not found: {boldItalicName}");
                    }

                    _appManager._config["DefaultFont"] = fontSelected;
                    _appManager.SaveConfig();
                }
                else
                    GD.PrintErr("FontSelected not found");

                for (int i = 0; i < fontSubmenu.ItemCount; i++)
                    fontSubmenu.SetItemChecked(i, false);

                fontSubmenu.ToggleItemChecked((int)index);


            }
            catch (Exception e)
            {
                GD.PrintErr("Font application failed: ", e);
            }

        }


            // Handles ModelButton menu item selections for model switching
        public void OnModelButtonIndexPressed(long index)
        {
                    // Ensure AppManager and ModelLabel are initialized to prevent null reference errors
            if (_appManager == null)
            {
                GD.PrintErr("AmiMain:ModelButtonIndexPressed - Error: AppManager is null");
                return;
            }
            if (_modelLabel == null)
            {
                GD.PrintErr("AmiMain:ModelButtonIndexPressed - Error: ModelLabel is null");
                return;
            }

            // Get the ModelButton popup to manage check states and retrieve model name
            var popup = GetNode<MenuButton>("BGPanelCont/TopContainer/MenuBarContainer/ModelButton").GetPopup();

            // Validate index and get model name
            if (index < 0 || index >= popup.ItemCount)
            {
                GD.PrintErr($"AmiMain:ModelButtonIndexPressed - Error: Invalid ModelButton index {index}");
                return;
            }
            string modelName = popup.GetItemText((int)index);

            try
            {
                // Uncheck all items (indices 0-5)
                for (int i = 0; i < popup.ItemCount; i++)
                {
                    popup.SetItemChecked(i, false);
                }

                // Check the selected item
                popup.SetItemChecked((int)index, true);

                // Check if model changed to decide on SaveConfig
                bool modelChanged = _modelName != modelName;

                // Update model name and config
                _modelName = modelName;
                _appManager._config["ModelSelected"] = modelName;

                // Extract current connectivity image from ModelLabel.Text
                string currentIcon = "disconnect_icon.png"; // Default to disconnected
                if (_modelLabel.Text.Contains("connect_icon.png"))
                {
                    currentIcon = "connect_icon.png";
                }

                // Update ModelLabel with the current image and new model name
                _modelLabel.Text = $"[img=40%]res://Assets/{currentIcon}[/img] {modelName}";

                // Save config only and update _currentModelData if model changed
                if (modelChanged)
                {
                    UpdateModelData();

                    if (_appManager.SaveConfig())
                    {
                        //GD.Print($"AmiMain:ModelButtonIndexPressed - Saved config with ModelSelected: {modelName}");
                    }
                    else
                    {
                        GD.PrintErr("AmiMain:ModelButtonIndexPressed - Error: SaveConfig failed");
                    }
                }

                // Log successful model selection
                GD.Print($"AmiMain:ModelButtonIndexPressed - Selected model {modelName}, vision = {_currentModelData["supports_vision"]}");
            }
            catch (Exception e)
            {
                GD.PrintErr($"AmiMain:ModelButtonIndexPressed - Error: {e.Message}");
            }
        }

        // Handles ToolsButton menu item selection
        private void OnToolsButtonIndexPressed(long index)
        {
            try
            {
                switch (index)
                {
                    case 0: //All tools; detect current this checked, set to opposite, set all tool items checked (opposite), set all  tool configs to "opposite", save config
                        
                        bool allCheckedChange = !_toolsButton.GetPopup().IsItemChecked(0);
                        _toolsButton.GetPopup().SetItemChecked(0, allCheckedChange);
                        _toolsButton.GetPopup().SetItemChecked(1, allCheckedChange);
                        _toolsButton.GetPopup().SetItemChecked(2, allCheckedChange);
                        _toolsButton.GetPopup().SetItemChecked(3, allCheckedChange);

                        _appManager._config["EnableCollectionSearch"] = allCheckedChange ? "true" : "false";
                        _appManager._config["EnableWebSearch"] = allCheckedChange ? "true" : "false";
                        _appManager._config["EnableCodeExecution"] = allCheckedChange ? "true" : "false";

                        //GD.Print("ToolsButton case 0: All tools set ", allCheckedChange);

                        if (!_appManager._config.TryGetValue("CollectionID", out var collIDobj) || collIDobj.ToString() == "none")
                            _amiTextEntry.CallTextEntryByUseCase(9);

                        _appManager.SaveConfig();

                        break;

                    case 1://CollectionSearch
                           //check _config["CollectionID"]; if null ot none, call _amiTextEntry.CallTextEntryByUseCase(9)
                           //check checked; set opposite; set _config{"EnableCollectionSearch"] "opposite", SaveConfig

                        if (!_appManager._config.TryGetValue("CollectionID", out var collIDobject) || collIDobject.ToString() == "none")
                            _amiTextEntry.CallTextEntryByUseCase(9);

                        _toolsButton.GetPopup().SetItemChecked(1, !_toolsButton.GetPopup().IsItemChecked(1));
                        _appManager._config["EnableCollectionSearch"] = _toolsButton.GetPopup().IsItemChecked(1) ? "true" : "false";
                        _toolsButton.GetPopup().SetItemChecked(0, (_toolsButton.GetPopup().IsItemChecked(1) && _toolsButton.GetPopup().IsItemChecked(2) && _toolsButton.GetPopup().IsItemChecked(3)));
                        //GD.Print($"OnToolsButton case 1, EnableCollectionSearch set {_appManager._config["EnableCollectionSearch"]}");

                        _appManager.SaveConfig();

                        break;

                    case 2: //WebSearch; check checked; set opposite; set _config["EnableWebSearch"] "opposite", SaveConfig

                        _toolsButton.GetPopup().SetItemChecked(2, !_toolsButton.GetPopup().IsItemChecked(2));
                        _appManager._config["EnableWebSearch"] = _toolsButton.GetPopup().IsItemChecked(2) ? "true" : "false";
                        _toolsButton.GetPopup().SetItemChecked(0, (_toolsButton.GetPopup().IsItemChecked(1) && _toolsButton.GetPopup().IsItemChecked(2) && _toolsButton.GetPopup().IsItemChecked(3)));
                        //GD.Print($"OnToolsButton case 2, EnableWebSearch set {_appManager._config["EnableWebSearch"]}");

                        _appManager.SaveConfig();

                        break;

                    case 3: //CodeExecution; check checked; set opposite; set _config["EnableCodeExecution"] "opposite", SaveConfig

                        _toolsButton.GetPopup().SetItemChecked(3, !_toolsButton.GetPopup().IsItemChecked(3));
                        _appManager._config["EnableCodeExecution"] = _toolsButton.GetPopup().IsItemChecked(3) ? "true" : "false";
                        _toolsButton.GetPopup().SetItemChecked(0, (_toolsButton.GetPopup().IsItemChecked(1) && _toolsButton.GetPopup().IsItemChecked(2) && _toolsButton.GetPopup().IsItemChecked(3)));
                        //GD.Print($"OnToolsButton case 1, EnableCodeExecution set {_appManager._config["EnableWebSearch"]}");

                        _appManager.SaveConfig();
                        break;

                    default: //invalid case, log

                        break;

                }

            }
            catch (Exception e)
            {
                GD.PrintErr("Tools selectioon failed: ", e);
            }
        }

            // Handles InformationButton menu item selections for xAI console and API key display
            public void OnInformationButtonIndexPressed(long index)
        {
            // Ensure AppManager and OutputLabel are initialized to prevent null reference errors
            if (_appManager == null)
            {
                GD.PrintErr("AmiMain:OnInformationButtonIndexPressed - Error: AppManager is null");
                return;
            }
            if (_outputLabel == null)
            {
                GD.PrintErr("AmiMain:OnInformationButtonIndexPressed - Error: OutputLabel is null");
                return;
            }

            // Handle operations based on menu item index
            switch (index)
            {
                case 0: // Your xAI Console
                    try
                    {
                        OS.ShellOpen("https://console.x.ai/");
                        //GD.Print("AmiMain:OnInformationButtonIndexPressed - Opened xAI Console URL");
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiMain:OnInformationButtonIndexPressed - Error opening xAI Console URL: {e.Message}");
                    }
                    break;

                case 1: // Your GrokAPI Key
                    try
                    {
                        //_amiTextEntryCalledBy = (int)index;
                        _amiTextEntry.CallTextEntryByUseCase((int)index);
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiMain:OnInformationButtonIndexPressed - Error updating API key: {e.Message}");
                        _outputLabel.Text = "Error updating API key.";
                    }
                    break;

                case 2: // Modify user information
                    try
                    {
                        string currentUserPrompt = Godot.FileAccess.GetFileAsString("user://Assets/user.txt");
                        _outputLabel.Text = $"[code]AMI> This is your current user information:\n [p]{currentUserPrompt}[/p]\n\nYou can edit it in the User Input window below.  Press the Send button when done.[/code]";
                        _userInputTextEdit.Text = currentUserPrompt;
                        _switchIndex = 5; //Set switch index to indicate next Send button press is for user info update
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiMain:OnInformationButtonIndexPressed - Error updating user prompt: {e.Message}");
                        _outputLabel.Text = "Error updating user information.";
                    }
                    break;

                case 3: // Custom Instructions; show and edit user://Assets/custom.txt
                    try
                    {
                        string currentCustomInstructions = Godot.FileAccess.GetFileAsString("user://Assets/custom.txt");
                        _outputLabel.Text = $"[code]Ami> These are your current custom instructions. You can edit them in the User Input window below.  Remember, all the text you add here will be added to the total context window.\n\n Press the Send button when done:\n [p]{currentCustomInstructions}[/p][/code]\n\n";
                        _userInputTextEdit.Text = currentCustomInstructions;
                        _switchIndex = 6; //Set switch index to indicate next Send button press is for custom instructions update
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiMain:OnInformationButtonIndexPressed case 3- Error updating custom instructions: {e.Message}");
                        _outputLabel.Text = "Error updating custom instructions.";
                    }
                    break;

                case 4: // Manage Collections; open user's Collections page in console

                    try
                    {
                        OS.ShellOpen("https://docs.x.ai/docs/guides/using-collections");
                        //GD.Print("AmiMain:OnInformationButtonIndexPressed - Opened xAI Console URL");
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr("Info case 4 failed to open Collections: ", e);
                    }
                    break;

                case 5:
                    try
                    {
                        _amiTextEntry.CallTextEntryByUseCase(9);
                        _appManager.SaveConfig();
                        //GD.Print("InfoButton case 5: CollectionID saved");

                    }
                    catch (Exception e)
                    {
                        GD.PrintErr("InfoButton case 5: Set CollectionID failed: ", e);
                            }
                    break;

                default:
                    GD.PrintErr($"AmiMain:OnInformationButtonIndexPressed - Error: Invalid index {index}");
                    break;
            }
        }

        // Handles HelpButton menu item selections for displaying help content and opening documentation URLs
        public void OnHelpButtonIndexPressed(long index)
        {
            // Ensure required nodes are initialized to prevent null reference errors
            if (_amiDisplay == null)
            {
                GD.PrintErr("AmiMain:OnHelpButtonIndexPressed - Error: AmiDisplay is null");
                return;
            }
            if (_outputLabel == null)
            {
                GD.PrintErr("AmiMain:OnHelpButtonIndexPressed - Error: OutputLabel is null");
                return;
            }

            // Handle operations based on menu item index
            switch (index)
            {
                case 0: // Using AMI
                    try
                    {
                        var filePath = "res://Assets/Using_AMI.txt";
                        if (Godot.FileAccess.FileExists(filePath))
                        {
                            var fileContent = Godot.FileAccess.GetFileAsString(filePath);
                            _amiDisplay.Visible = true;
                            _amiDisplay.Title = "Using AMI";
                            _amiDisplay._displayLabel.Text = fileContent;
                            //GD.Print("AmiMain:OnHelpButtonIndexPressed - Displayed Using_AMI.txt");
                        }
                        else
                        {
                            _amiDisplay.Visible = true;
                            _amiDisplay.Title = "Using AMI";
                            _amiDisplay._displayLabel.Text = "Help file not found.";
                            GD.PrintErr("AmiMain:OnHelpButtonIndexPressed - Using_AMI.txt not found");
                        }
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiMain:OnHelpButtonIndexPressed - Error reading Using_AMI.txt: {e.Message}");
                        _amiDisplay.Visible = true;
                        _amiDisplay.Title = "Using AMI";
                        _amiDisplay._displayLabel.Text = "Error loading help file.";
                    }
                    break;

                case 1: // About Tokens and Spend
                    try
                    {
                        OS.ShellOpen("https://docs.x.ai/docs/key-information/consumption-and-rate-limits");
                        //GD.Print("AmiMain:OnHelpButtonIndexPressed - Opened Tokens and Spend URL");
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiMain:OnHelpButtonIndexPressed - Error opening Tokens and Spend URL: {e.Message}");
                    }
                    break;

                case 2: // Using GrokAPI
                    try
                    {
                        OS.ShellOpen("https://docs.x.ai/docs/guides/chat");
                        //GD.Print("AmiMain:OnHelpButtonIndexPressed - Opened Using GrokAPI URL");
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiMain:OnHelpButtonIndexPressed - Error opening Using GrokAPI URL: {e.Message}");
                    }
                    break;

                case 3: // About AMI
                    try
                    {
                        var filePath = "res://Assets/About_AMI.txt";
                        if (Godot.FileAccess.FileExists(filePath))
                        {
                            var fileContent = Godot.FileAccess.GetFileAsString(filePath);
                            _outputLabel.Text += fileContent;
                            //GD.Print("AmiMain:OnHelpButtonIndexPressed - Displayed About_AMI.txt");
                        }
                        else
                        {
                            _outputLabel.Text += "[p]About AMI file missing or corrupted.[/p]";
                            GD.PrintErr("AmiMain:OnHelpButtonIndexPressed - About_AMI.txt not found");
                        }
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiMain:OnHelpButtonIndexPressed - Error reading About_AMI.txt: {e.Message}");
                        _outputLabel.Text += "[p]Error loading About AMI file.[/p]";
                    }
                    break;

                case 4:
                    try
                    {
                        OS.ShellOpen("https://docs.x.ai/docs/key-information/collections");
                        //GD.Print("AmiMain:OnHelpButtonIndexPressed - Opened Collections URL");

                    }
                    catch (Exception e)
                    {
                        GD.PrintErr("HelpButton case 4: Failed to open Collections docs: ", e);
                    }
                    break;

                default:
                    GD.PrintErr($"AmiMain:OnHelpButtonIndexPressed - Error: Invalid index {index}");
                    break;
            }
        }

        private void OnQuitButtonPressed()
        {
            // Save config and exit
            if (_appManager.SaveConfig())
            {
                GD.Print("AmiMain:OnQuitButtonPressed - Saved config");
            }
            else
            {
                GD.PrintErr("AmiMain:OnQuitButtonPressed - Error: SaveConfig failed");
            }

            try
            {
                RemoveChild(_amiDisplay);
                _amiDisplay.Free();
                //GD.Print("AMI_Display freed");
                RemoveChild(_amiThink);
                _amiThink.Free();
                //GD.Print("AMI_Think freed");
                RemoveChild(_amiTextEntry);
                _amiTextEntry.Free();
                //GD.Print("AMI_TextEntry freed");
            }
            catch (Exception e)
            {
                GD.PrintErr("QuitButton: Scene freeing failed: ", e);
            }

            try
            {
                Theme.DefaultFont = null;
                //GD.Print("QuitButton: Font freed");

                Theme = null;
                //GD.Print("QuitButton: Theme freed");
            }
            catch (Exception e)
            {
                GD.PrintErr("QuitButton:  Theme or font free failed: ", e);
            }

            try
            {
                VoiceManager.Singleton?.StopAndCleanup();
                //this.Free();
                GD.Print($"Main OnQuitButtonPressed: Quit button pressed. Freed main.");
                GetTree().Quit();
            }
            catch (Exception e)
            {
                GD.PrintErr("Freeing main scene then quit failed.  Force quit: ",e);
                GetTree().Dispose();
            }
        }

        //Handles localization selection
        private async void OnLocalizationButtonIndexPressed(long index)
        {
            try
            {
                //Set localization state
                _appManager.LocalizeTo(index);

                _amiThink.Visible = true;
                _amiThink._anim.Active = true;
                _amiThink._anim.Play("think");

                //Create response from model to confirm localization
                _lastRequestType = "user_query";
                var (messages, responseOptions) = _appManager.WritePrompt($"Localization has been reset to {_localizationButton.GetItemText((int)index)}. Please acknowledge readiness in that language.");

                OpenAIResponseClient localClient = _responsesClient.GetOpenAIResponseClient(_appManager._config["ModelSelected"].ToString());
                var result = await localClient.CreateResponseAsync(messages, responseOptions);
                if (result != null)
                    _appManager.ParseResponsesAPIResponse(result.Value, true);
                else
                    GD.PrintErr("AmiMain:OnSendButtonPressed - No response received from CreateResponseAsync");


                _outputLabel.AppendText($"Localization set to {_localizationButton.GetItemText((int)index)}"); 
            }
            catch (Exception e)
            {
                GD.PrintErr("OnLocalizationButtonIndexPressed failed: ", e);
            }
        }

        //Handle clicks on ArtifactsList items to show the files in _amiDisplay._displayLabel
        private void OnArtifactsListItemSelected(int index)
        {
            // Ensure required nodes are initialized
            if (_artifactsList == null)
            {
                GD.PrintErr("AmiMain:OnArtifactsListItemSelected - Error: ArtifactsList is null");
                return;
            }
            if (_amiDisplay == null)
            {
                GD.PrintErr("AmiMain:OnArtifactsListItemSelected - Error: AmiDisplay is null");
                return;
            }
            try
            {
                _artifactsList.SetItemSelectable(index, true);

                // Get selected item text
                string selectedItem = _artifactsList.GetItemText(index);
                if (string.IsNullOrEmpty(selectedItem))
                {
                    GD.PrintErr("AmiMain:OnArtifactsListItemSelected - Error: Selected item text is empty");
                    return;
                }
                // Construct full file path
                var filePath = Path.Combine("user://Session_Outputs/", selectedItem);
                _viewFilePath = ProjectSettings.GlobalizePath("user://Session_Outputs/");
                if (!Godot.FileAccess.FileExists(filePath))
                {
                    GD.PrintErr($"AmiMain:OnArtifactsListItemSelected - Error: File not found at {filePath}");
                    return;
                }
                // Read file content and display in AmiDisplay
                var fileContent = Godot.FileAccess.GetFileAsString(filePath);
                _amiDisplay.Visible = true;
                _viewFileName = selectedItem;
                _amiDisplay.Title = $"Viewing {selectedItem}";
                _amiDisplay._displayLabel.Text = fileContent;
                //GD.Print($"AmiMain:OnArtifactsListItemSelected - Displayed content of {selectedItem} in Display Window");
            }
            catch (Exception e)
            {
                GD.PrintErr($"AmiMain:OnArtifactsListItemSelected - Error displaying file content: {e.Message}");
            }
        }

        //Handle clicks on UploadsList items to show the files in _amiDisplay._displayLabel
        private void OnUploadsListItemSelected(int index)
        {
            // Ensure required nodes are initialized
            if (_uploadsList == null)
            {
                GD.PrintErr("AmiMain:OnUploadsListItemSelected - Error: UploadsList is null");
                return;
            }
            if (_amiDisplay == null)
            {
                GD.PrintErr("AmiMain:OnUploadsListItemSelected - Error: AmiDisplay is null");
                return;
            }
            try
            {
               _uploadsList.SetItemSelectable(index, true);

                // Get selected item text
                string selectedItem = _uploadsList.GetItemText(index);
                if (string.IsNullOrEmpty(selectedItem))
                {
                    GD.PrintErr("AmiMain:OnUploadsListItemSelected - Error: Selected item text is empty");
                    return;
                }
                // Construct full file path
                var filePath = Path.Combine("user://Uploaded_Files/", selectedItem);
                if (!Godot.FileAccess.FileExists(filePath))
                {
                    GD.PrintErr($"AmiMain:OnUploadsListItemSelected - Error: File not found at {filePath}");
                    return;
                }
                // Read file content and display in AmiDisplay
                var fileContent = Godot.FileAccess.GetFileAsString(filePath);
                _amiDisplay.Visible = true;
                _amiDisplay.Title = $"Viewing {selectedItem}";
                _amiDisplay._displayLabel.Text = fileContent;
                //GD.Print($"AmiMain:OnUploadsListItemSelected - Displayed content of {selectedItem} in Display Window");
            }
            catch (Exception e)
            {
                GD.PrintErr($"AmiMain:OnUploadsListItemSelected - Error displaying file content: {e.Message}");
            }
        }

        // Handles UploadButton Pressed signal to select a text-based file for prompt inclusion
        public void OnUploadButtonPressed()
        {
            // Ensure required nodes are initialized
            if (_appManager == null)
            {
                GD.PrintErr("AmiMain:OnUploadButtonPressed - Error: AppManager is null");
                return;
            }
            if (_uploadsList == null)
            {
                GD.PrintErr("AmiMain:OnUploadButtonPressed - Error: UploadsList is null");
                return;
            }
            if (_outputLabel == null)
            {
                GD.PrintErr("AmiMain:OnUploadButtonPressed - Error: OutputLabel is null");
                return;
            }

            try
            {
                // Initialize FileDialog for native Windows File Explorer
                string startDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
                //GD.Print($"AmiMain:OnUploadButtonPressed - Starting directory for FileDialog: {startDir}");
                var fileDialog = new FileDialog
                {
                    FileMode = FileDialog.FileModeEnum.OpenFile,
                    Access = FileDialog.AccessEnum.Filesystem,
                    UseNativeDialog = true
                   
                };
                fileDialog.AddFilter("*.txt, *.json, *.md, *.cc, *.cpp, *.cs, *.py, *.html, *.css, *.xaml, *.gd, *.csv, *.tscn; text/docs", "Documents");
                fileDialog.AddFilter("*.png, *.jpg, *.jpeg, *.gif; image/png, image/jpeg, image/gif", "Images");
                fileDialog.SetCurrentDir(startDir);
                fileDialog.FileSelected += OnUploadFileDialogSelected;
                AddChild(fileDialog);
                fileDialog.Popup();
                //GD.Print("AmiMain:OnUploadButtonPressed - Opened FileDialog for file selection");
            }
            catch (Exception e)
            {
                GD.PrintErr($"AmiMain:OnUploadButtonPressed - Error opening FileDialog: {e.Message}");
                _outputLabel.Text += "[p]Error opening file selector.[/p]";
            }
        }

        // Handles SendButton Pressed signal to process API key input or send prompt based on _apiKeyExists
        public async void OnSendButtonPressed()
           {
            // Ensure required nodes are initialized to prevent null reference errors
            if (_userInputTextEdit == null)
            {
                GD.PrintErr("AmiMain:OnSendButtonPressed - Error: UserInputTextEdit is null");
                return;
            }
            if (_outputLabel == null)
            {
                GD.PrintErr("AmiMain:OnSendButtonPressed - Error: OutputLabel is null");
                return;
            }

            // Determine switch index based on cached.json exists
            if (Godot.FileAccess.FileExists("user://Context/cached.json") && _switchIndex < 2)
                _switchIndex = 0; // API Key exists
            else if (!Godot.FileAccess.FileExists("user://Context/cached.json") && _switchIndex < 2)
                _switchIndex = 1; // No API Key

            if (VoiceManager.Singleton?._isPlaying == true)
            {
                VoiceManager.Singleton.StopAndCleanup();
                _outputLabel.Text += "[code]AMI> Voice playback stopped for new query.[/code]";
                return;
            }

            // Handle operations based on switch index
            switch (_switchIndex)
            {
                case 0: // API Key exists, send user query
                    try
                    {
                        // Validate nodes
                        if (_userInputTextEdit == null)
                        {
                            GD.PrintErr("AmiMain:OnSendButtonPressed - Error: UserInputTextEdit is null");
                            return;
                        }

                        //Check SessionRequiresVision vs _currentModelData["supports_vision"]; if SessionRequiresVision is true and model does not support vision, output error and return
                        try
                        {
                            if (_currentModelData.TryGetValue("supports_vision", out var visionSupport) && visionSupport.ToString() == "false")
                            {

                                string modelVision = _currentModelData["supports_vision"].ToString();
                                string sessionVision = _appManager._config["SessionRequiresVision"].ToString();
                                if (sessionVision == "true" && modelVision != "true")
                                {
                                    _outputLabel.Text += "[code]AMI> Error: Current model does not support vision inputs. Please select a different model or start a new session.[/code]";
                                    return;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            GD.PrintErr("AmiMain:OnSendButtonPressed - Error checking vision support: ", e);
                        }

                        // Set request type and get user input
                        _lastRequestType = "user_query";
                        var userInput = _userInputTextEdit.Text;

                        // Generate prompt, get response

                        var (messages, responseOptions) = _appManager.WritePrompt(userInput);

                        //Start thinking animation
                        if (_amiThink != null)
                        {
                            _amiThink.Visible = true;
                            _amiThink._anim.Active = true;
                            _amiThink._anim.Play("think");

                           //GD.Print("AmiMain.SendButton: AmiThink visible = ,", _amiThink.Visible,  " at ", DateTime.Now.ToString("hhmm:ss.fff"));
                        }
                        else
                            GD.PrintErr("AMiMain.SendButton: Think Animation failed - AmiThink not found");

                        //GD.Print("AmiMain:OnSendButtonPressed sending ", messages);                       
                        OpenAIResponseClient localClient = _responsesClient.GetOpenAIResponseClient(_appManager._config["ModelSelected"].ToString());
                       
                        var result = await localClient.CreateResponseAsync(messages, responseOptions);
                        if (result != null)
                            _appManager.ParseResponsesAPIResponse(result.Value, true);
                        else
                            GD.PrintErr("AmiMain:OnSendButtonPressed - No response received from CreateResponseAsync");

                        // Clear input
                        _userInputTextEdit.Text = "";
                        //GD.Print("AmiMain:OnSendButtonPressed - Sent user query");
                    }
                    catch (Exception e)
                    {
                        _amiThink._anim.Active = false;
                        _amiThink.Visible = false;
                        GD.PrintErr($"AmiMain:OnSendButtonPressed - Error: {e.Message}");
                        _outputLabel.Text += $"[p]\n\nError sending query: {e.Message}.  Please try a different query.[/p]";
                    }
                    break;

                case 1: // Check API Key; if no API Key, process input as API key
                    //GD.Print("OnSendButtonPressed: Processing API key input, appManager._apiKey =", _appManager._apiKey);
                    try
                    {
                        if (_appManager._apiKey == null || _appManager._apiKey == "")
                        {
                            // Select and retrieve API key from UserInputTextEdit
                            _userInputTextEdit.SelectAll();
                            string apiKey = _userInputTextEdit.GetSelectedText();
                            //GD.Print("AmiMain:OnSendButtonPressed - Retrieved API key input, ", apiKey);

                            // Validate input
                            if (string.IsNullOrWhiteSpace(apiKey))
                            {
                                GD.PrintErr("AmiMain:OnSendButtonPressed - Error: Invalid API key input");
                                _outputLabel.Text = "Invalid API key. Please enter a valid key and press Send.";
                                return;
                            }

                            // Encode and save API key; initialize responseClient because skipped in _Ready
                            _appManager._apiKey = apiKey;
                            OpenAIClientOptions options = new() { Endpoint = new Uri("https://api.x.ai/v1/") };
                            _responsesClient = new OpenAIClient(new ApiKeyCredential(_appManager._apiKey), options);
                            var apiKeyPath = "user://Context/cached.json";
                            var encodedKey = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(apiKey));
                            using var file = Godot.FileAccess.Open(apiKeyPath, Godot.FileAccess.ModeFlags.Write);
                            if (file != null)
                            {
                                file.StoreString(encodedKey);
                                //_apiKeyExists = true;
                                _switchIndex = 0; //reset to user input mode
                                _userInputTextEdit.Text = "";
                                _outputLabel.Text = "[code]AMI> API key saved successfully. You may now start a new session.[/code]";
                                //GD.Print("AmiMain:OnSendButtonPressed - Saved API key.  _switchIndex = 0");
                            }
                            else
                            {
                                GD.PrintErr($"AmiMain:OnSendButtonPressed - Error: FileAccess null for {apiKeyPath}");
                                _outputLabel.Text = "Error saving API key. Please try again.";
                            }
                            //Restart app
                            _appManager.LoadConfig();
                            _appManager._config["PersonaSelected"] = "Assistant";
                            _appManager.SaveConfig();
                            GetTree().ReloadCurrentScene();
                        }
                        else
                        {
                            GD.PrintErr("AmiMain:OnSendButtonPressed - _appManager._api is not null or blank");
                            _outputLabel.Text = "API key not null or blank.";
                        }
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiMain:OnSendButtonPressed - Error saving API key: {e.Message}");
                        _outputLabel.Text = "Error saving API key. Please try again.";
                    }
                    break;

                case 2: // Process name and solicit age
                    try
                    {
                        if (_userInputTextEdit.Text.Length > 0)
                        {
                            _userInputTextEdit.SelectAll();
                            _userNameInfo = _userInputTextEdit.GetSelectedText();
                        }
                        _outputLabel.Text = "\n\n[code]AMI> Great! It's optional, but it will help Grok interact with you to know your age or generation cohort.  Enter what you'd like below and press Send, or just press Send to decline.[/code]";
                        _switchIndex = 3; // Move to next case
                        _userInputTextEdit.Text = "";
                        //GD.Print($"AmiMain:OnSendButtonPressed - Processed name input {_userNameInfo}, switchIndex set to {_switchIndex}");
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiMain:OnSendButtonPressed - Error processing name input: {e.Message}");
                    }
                    break;

                case 3: // process age and solicit facts
                    try
                    {
                        if (_userInputTextEdit.Text.Length > 0)
                        {
                            _userInputTextEdit.SelectAll();
                            _userAgeInformation = _userInputTextEdit.GetSelectedText();
                        }
                        _outputLabel.Text = "\n\n[code]AMI> Thank you! If you'd like to provide any additional facts about yourself to help Grok better assist you, please enter them below and press Send.  Otherwise, just press Send to decline.\n\nRemember, you can always get help about using AMI by clicking Help > Using AMI in the menu bar at the top.[/code]";
                        _switchIndex = 4; // Move to next case
                        _userInputTextEdit.Text = "";
                        //GD.Print($"AmiMain:OnSendButtonPressed - Processed age input {_userAgeInformation}, switchIndex set to {_switchIndex}");
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiMain:OnSendButtonPressed - Error processing age input: {e.Message}");
                    }
                    break;

                case 4: // process additional facts, create user://Assets/user.txt, solicit API key, and proceed to case 1 to process API key and start session 
                    string userFactsInformation = "No additional user facts provided; make no inferences unless user specifies";

                    try
                    {
                        if (_userInputTextEdit.Text.Length > 0)
                        {
                            _userInputTextEdit.SelectAll();
                            userFactsInformation = _userInputTextEdit.GetSelectedText();
                        }
                        _outputLabel.Text = "\n\n[code]AMI> Thank you! Please enter your GrokAPI key below and press Send to start your session.  If you do not have a GrokAPI key, please visit [url=https://console.x.ai/]your xAI console[/url] to create one. Remember, never input financial information directly into AMI.[/code]";
                        _switchIndex = 1; // Move to API key input case
                        _userInputTextEdit.Text = "";
                        using var userFile = Godot.FileAccess.Open("user://Assets/user.txt", Godot.FileAccess.ModeFlags.Write);
                        userFile.StoreString($"Call the user {_userNameInfo}, but vary address in a natural conversational manner;  user's age is {_userAgeInformation}. Additional facts about the user: {userFactsInformation}");

                        //GD.Print($"AmiMain:OnSendButtonPressed - Processed additional facts input {userFactsInformation}, switchIndex set to {_switchIndex}");
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiMain:OnSendButtonPressed - Error processing additional facts input: {e.Message}");
                    }
                    break;

                case 5: // Update user information file

                    try
                    {
                        if (_userInputTextEdit.Text.Length > 0)
                        {
                            //Update local user file
                            _userInputTextEdit.SelectAll();
                            string updatedUserInfo = _userInputTextEdit.GetSelectedText();
                            using var userFile = Godot.FileAccess.Open("user://Assets/user.txt", Godot.FileAccess.ModeFlags.Write);
                            userFile.StoreString(updatedUserInfo);
                            _outputLabel.Text = "[code]AMI> User information updated successfully.[/code]";
                            _userInputTextEdit.Text = "";
                            _switchIndex = 0; //reset to user query mode

                            //send update confirmation to model
                            _amiThink.Visible = true;
                            _amiThink._anim.Active = true;
                            _amiThink._anim.Play("think");

                            try
                            {
                                _lastRequestType = "user_query";
                                var (messages, responseOptions) = _appManager.WritePrompt($"My user information has been updated successfully: {updatedUserInfo}.  Please acknowledge and use this information in place of the system prompt in susbsequent responses.");
                                OpenAIResponseClient localClient = _responsesClient.GetOpenAIResponseClient(_appManager._config["ModelSelected"].ToString());
                                var result = await localClient.CreateResponseAsync(messages, responseOptions);
                                if (result != null)
                                    _appManager.ParseResponsesAPIResponse(result.Value, true);
                                else
                                    GD.PrintErr("AmiMain:OnSendButtonPressed case 5 - No response received from CreateResponseAsync");



                                GD.Print("AmiMain:OnSendButtonPressed - Updated user information file successfully.");
                            }
                            catch (Exception e)
                            {
                                _amiThink._anim.Active = false;
                                _amiThink.Visible = false;
                                _outputLabel.Text += $"[p]\n\nError sending updated user info confirmation to model: {e.Message}.[/p]";
                                GD.PrintErr($"AmiMain:OnSendButtonPressed - Error sending updated user info confirmation to model: {e.Message}");
                            }

                        }
                        else
                        {
                            GD.PrintErr("AmiMain:OnSendButtonPressed - Error: No user information input to update.");
                            _outputLabel.Text = "No user information input to update.";
                        }
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiMain:OnSendButtonPressed - Error updating user information file: {e.Message}");
                        _outputLabel.Text = "Error updating user information.";
                    }
                    break; 

                case 6: // Update custom instructions file
                    try
                    {
                        if (_userInputTextEdit.Text.Length > 0)
                        {
                            //Update local custom instructions file
                            _userInputTextEdit.SelectAll();
                            string updatedCustomInstructions = _userInputTextEdit.GetSelectedText();
                            using var customFile = Godot.FileAccess.Open("user://Assets/custom.txt", Godot.FileAccess.ModeFlags.Write);
                            customFile.StoreString(updatedCustomInstructions);
                            _outputLabel.Text = "[code]AMI> Custom instructions updated successfully.[/code]";
                            _userInputTextEdit.Text = "";
                            _switchIndex = 0; //reset to user query mode
                            GD.Print("AmiMain:OnSendButtonPressed - Updated custom instructions file successfully.");


                            //send update confirmation to model
                            _amiThink.Visible = true;
                            _amiThink._anim.Active = true;
                            _amiThink._anim.Play("think");

                            try
                            {
                                _lastRequestType = "user_query";
                                var (messages, responseOptions) = _appManager.WritePrompt($"My custom instructions have been updated successfully: {updatedCustomInstructions}.  Please acknowledge and use this information in place of the custom instructions in the system prompt in susbsequent responses.");
                                OpenAIResponseClient localClient = _responsesClient.GetOpenAIResponseClient(_appManager._config["ModelSelected"].ToString());
                                var result = await localClient.CreateResponseAsync(messages, responseOptions);
                                if (result != null)
                                    _appManager.ParseResponsesAPIResponse(result.Value, true);
                                else
                                    GD.PrintErr("AmiMain:OnSendButtonPressed case 5 - No response received from CreateResponseAsync");



                                //GD.Print("AmiMain:OnSendButtonPressed - Updated user information file successfully.");
                            }
                            catch (Exception e)
                            {
                                _amiThink._anim.Active = false;
                                _amiThink.Visible = false;
                                _outputLabel.Text += $"[p]\n\nError sending updated user info confirmation to model: {e.Message}.[/p]";
                                GD.PrintErr($"AmiMain:OnSendButtonPressed - Error sending updated user info confirmation to model: {e.Message}");
                            }
                        }

                        else
                        {
                            GD.PrintErr("AmiMain:OnSendButtonPressed - Error: No custom instructions input to update.");
                            _outputLabel.Text = "No custom instructions input to update.";
                        }
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr($"AmiMain:OnSendButtonPressed - Error updating custom instructions file: {e.Message}");
                        _outputLabel.Text = "Error updating custom instructions.";
                    }
                    break;
                default:
                    GD.PrintErr($"AmiMain:OnSendButtonPressed - Error: Invalid switchIndex {_switchIndex}");
                    break;
            }
        }

        // Handles UpdateButtonPressed signal to open AmiTextEntry for updating Budget and Threshold
        public void OnUpdateButtonPressed()
        {
            // Ensure required nodes are initialized
            if (_amiTextEntry == null)
            {
                GD.PrintErr("AmiMain:OnViewButtonPressed - Error: AmiFilesList is null");
                return;
            }
            if (_outputLabel == null)
            {
                GD.PrintErr("AmiMain:OnViewButtonPressed - Error: OutputLabel is null");
                return;
            }

            try
            {
                // Set dialog purpose for UpdateButton
                _amiTextEntry.CallTextEntryByUseCase(0);

                //GD.Print("AmiMain:OnUpdateButtonPressed - Opened AmiTextEntry");
            }
            catch (Exception e)
            {
                GD.PrintErr($"AmiMain:OnUpdateButtonPressed - Error opening AmiTextEntry: {e.Message}");
                _outputLabel.Text += $"[p]Error opening dialog: {e.Message}[/p]";
            }
        }

         // Handles HttpRequest.RequestCompleted signal for Models endpoint requests
#pragma warning disable IDE0060 // Remove unused parameter
        public void OnHttpRequestCompleted(int result, int responseCode, string[] headers, byte[] body)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            try
            {
                // Convert response body to JSON string
                string json = Godot.FileAccess.GetFileAsString("user://API_Output/last_response.json");
                //GD.Print("AmiMain:OnHttpRequestCompleted - Received HTTP response: ", json);
                using var modelsResponse = Godot.FileAccess.Open("user://API_Output/ModelsResponse.json", Godot.FileAccess.ModeFlags.Write);
                modelsResponse.StoreString(json);
                //GD.Print("AmiMain:OnHttpRequestCompleted - Saved ModelsResponse.json");

                //Deserialize JSON to extract model names
                _modelsData = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                //prepare ModelButton for population
                var popup = _modelButton.GetPopup();
                popup.Clear();

                using var file = Godot.FileAccess.Open("user://API_Output/ModelsList.txt", Godot.FileAccess.ModeFlags.Write);
                if (_modelsData != null && _modelsData.TryGetValue("models", out var dataObj) && dataObj is JsonElement dataElement && dataElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var modelElement in dataElement.EnumerateArray())
                    {
                        if (modelElement.TryGetProperty("id", out var nameElement))
                        {
                            string modelName = nameElement.GetString();
                            var prompttokenprice = modelElement.GetProperty("prompt_text_token_price").GetDecimal();
                            var cachedtokenprice = modelElement.GetProperty("cached_prompt_text_token_price").GetDecimal();
                            var completiontokenprice = modelElement.GetProperty("completion_text_token_price").GetDecimal();
                            file.StoreString($"{modelName}: prompt price {prompttokenprice}, cached price {cachedtokenprice}, output price {completiontokenprice}\n");
                            //GD.Print($"AmiMain:OnHttpRequestCompleted - Found model: {modelName}");

                            // Extract modalities for vision support
                            string supportsVision = "false";
                            if (modelElement.TryGetProperty("input_modalities", out var modalitiesElement) && modalitiesElement.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var modality in modalitiesElement.EnumerateArray())
                                {
                                    if (modality.GetString() == "image")
                                    {
                                        supportsVision = "true";
                                        break;
                                    }
                                }
                            }


                            if (modelName == _appManager._config["ModelSelected"].ToString())
                            {
                                _currentModelData["model_name"] = modelName;
                                _currentModelData["prompt_token_price"] = prompttokenprice;
                                _currentModelData["cached_token_price"] = cachedtokenprice;
                                _currentModelData["output_token_price"] = completiontokenprice;
                                _currentModelData["supports_vision"] = supportsVision;
                                //GD.Print("AmiMain:OnHttpRequestCompleted - Current selected model found in models list: ", _currentModelData["model_name"].ToString(), _currentModelData["output_token_price"].ToString());
                            }

                            //Populate ModelButton Popup with id
                            popup.AddRadioCheckItem(modelName);
                        }
                        //.GD.Print("AmiMain:OnHttpRequestCompleted - Processed models list successfully");

                    }
                    
                     //Set correct item checked per _config["ModelSelected'], ensure all other unchecked
                        try
                        {
                        for (int i = 0; i < _modelButton.ItemCount; i++)
                        {
                            if (_modelButton.GetPopup().GetItemText(i) == _appManager._config["ModelSelected"].ToString())
                            {
                                _modelButton.GetPopup().SetItemChecked(i, true);
                                //GD.Print($"InitializeApp ModelButton index {i}, {modelButton.GetPopup().GetItemText(i)} checked.");
                            }
                            else
                            {
                                _modelButton.GetPopup().SetItemChecked(i, false);
                                //GD.Print($"InitializeApp ModelButton index {i}, {modelButton.GetPopup().GetItemText(i)} unchecked.");
                            }
                        }
                        }
                        catch (Exception e)
                        {

                        GD.PrintErr("InitializeApp: ModelButton initialization failed: ", e);
                        }
                }
                else
                {
                    GD.PrintErr("AmiMain:OnHttpRequestCompleted - Error: Invalid models data in response");
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"AmiMain:OnHttpRequestCompleted - Error processing HTTP response: {e.Message}");
            }
        }

        // Handles right-click on TextEdit to show spellcheck suggestions menu
        private void OnTextEditGuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Right && mouseEvent.Pressed)
            {
                var menu = _userInputTextEdit.GetMenu();
                //GD.Print("menu.IdPressed should be disconnected");
                _mousePos = mouseEvent.Position;
                var word = _userInputTextEdit.GetWordAtPos(_mousePos);
                //GD.Print("Selected word is ", word);

                if (!string.IsNullOrWhiteSpace(word))
                {
                    if (_appManager._spellDictionary != null && !_appManager._spellDictionary.Check(word))
                    {
                        menu = _userInputTextEdit.GetMenu();

                        //Clear menu items past Redo
                        menu.ItemCount = menu.GetItemIndex((int)TextEdit.MenuItems.Redo) + 1;

                        // Refactored to add suggestions to the existing menu using proper IDs
                        var suggestions = _appManager._spellDictionary.Suggest(word).Take(6).ToList();
                        if (suggestions.Count > 0)
                        {
                            menu.AddSeparator();
                            int baseId = (int)TextEdit.MenuItems.Max + 1;
                            foreach (var (suggestion, index) in suggestions.Select((s, i) => (s, i)))
                            {
                                menu.AddItem(suggestion, baseId + index);
                            }
                            menu.AddSeparator();
                        }
                        // Add "Add to Dictionary" option with unique ID
                        menu.AddItem("Add to Dictionary", (int)TextEdit.MenuItems.Max + 7);

                        // Store word for handler use
                        _currentMisspelledWord = word;
                        
                    }
                }
            }
        }

        // Updated handler to check for new IDs
        private void OnContextMenuOptionPressed(long idLong)
        {
            string lang = _appManager._config["ActiveLanguage"].ToString();
            int id = (int)idLong;
            if (string.IsNullOrWhiteSpace(_currentMisspelledWord))
                return;

            int max = (int)TextEdit.MenuItems.Max;
            if (id == max + 7) // Add to Dictionary
            {
                try
                {
                    var userDictPath = $"user://Assets/dicts/{lang}.dic"; // Adjust language as needed
                    using var file = Godot.FileAccess.Open(userDictPath, Godot.FileAccess.ModeFlags.ReadWrite);
                    if (file != null)
                    {
                        file.SeekEnd();
                        file.StoreString("\n" + _currentMisspelledWord);
                        _appManager._spellDictionary.Add(_currentMisspelledWord); // Add to runtime dictionary
                        //GD.Print($"AmiMain:OnMenuOptionPressed - Added '{_currentMisspelledWord}' to dictionary");
                    }
                    else
                    {
                        // Create file if it doesn't exist
                        using var newFile = Godot.FileAccess.Open(userDictPath, Godot.FileAccess.ModeFlags.Write);
                        newFile.StoreString(_currentMisspelledWord);
                        _appManager._spellDictionary.Add(_currentMisspelledWord);
                        //GD.Print($"AmiMain:OnMenuOptionPressed - Created and added '{_currentMisspelledWord}' to dictionary");
                    }
                }
                catch (Exception e)
                {
                    GD.PrintErr($"AmiMain:OnMenuOptionPressed - Error adding word: {e.Message}");
                }
            }
            else if (id >= max + 1 && id <= max + 6) // Replace with suggestion
            {
                var suggestions = _appManager._spellDictionary.Suggest(_currentMisspelledWord).Take(6).ToList();
                int index = id - (max + 1);
                if (index < suggestions.Count)
                {
                    // Replace word in text (basic implementation; expand for caret positioning if needed)
                    var text = _userInputTextEdit.Text;
                    var wordIndex = text.IndexOf(_currentMisspelledWord);
                    if (wordIndex >= 0)
                    {
                        _userInputTextEdit.Text = text.Remove(wordIndex, _currentMisspelledWord.Length).Insert(wordIndex, suggestions[index]);
                        //GD.Print($"AmiMain:OnMenuOptionPressed - Replaced '{_currentMisspelledWord}' with '{suggestions[index]}'");
                    }
                }
            }
            try
            {
                _userInputTextEdit.SetCaretLine(_userInputTextEdit.GetLineCount()-1);
                _userInputTextEdit.SetCaretColumn(_userInputTextEdit.GetLine(_userInputTextEdit.GetLineCount()-1).Length);
            }
            catch (Exception e)
            { 
                GD.PrintErr("OnContextMenuOptionPressed caret return failed: ", e); 
            }
            _currentMisspelledWord = null; // Clear

        }

        // Handles FileDialog FileSelected signal to process selected file
        private void OnFileDialogFileSelected(string path)
        {
            // Validate file path
            if (string.IsNullOrEmpty(path))
            {
                GD.PrintErr("AmiMain:OnFileDialogFileSelected - Error: No file selected");
                _outputLabel.Text += "[p]No file selected.[/p]";
                return;
            }

            try
            {
                // Store selected file path
                _filePath = path;
                var filename = Path.GetFileName(path);
            }
            catch (Exception e)
            {
                GD.PrintErr($"AmiMain:OnFileDialogFileSelected - Error processing file selection: {e.Message}");
                _outputLabel.Text += "[p]Error processing file selection.[/p]";
            }
        }

        // Handles FileDialog FileSelected signal for UploadButton to process file
        private void OnUploadFileDialogSelected(string path)
        {
            // Validate file path  
            if (string.IsNullOrEmpty(path))
            {
                GD.PrintErr("AmiMain:OnUploadFileDialogSelected - Error: No file selected");
                _outputLabel.Text += "No file selected."; // BBCode for styling  
                return;
            }

            try
            {
                string ext = System.IO.Path.GetExtension(path).ToLower();
                string filename = System.IO.Path.GetFileName(path);

                if (ext == ".txt" || ext == ".json" || ext == ".md" || ext == ".cs" || ext == ".py" || ext == ".html" || ext == ".css" || ext == ".cc" || ext == ".cpp" || ext == ".xaml" || ext == ".tscn") // Text/docs  
                {
                    // Existing text handling  
                    _appManager.StoreUploadedFile(path);
                    //GD.Print($"AmiMain:OnUploadFileDialogSelected - Processed text upload: {filename}");
                    _outputLabel.Text += $"Text file '{filename}' uploaded and ready for query.";
                }
                else if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif" || ext == ".webp") // Images  
                {
                    // Load raw bytes  
                    using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
                    if (file == null)
                    {
                        GD.PrintErr("Failed to open image file");
                    }

                    byte[] imageBytes = file.GetBuffer((int)file.GetLength()); // Full bytes  
                    long fileSizeMB = imageBytes.Length / (1024 * 1024);

                    if (fileSizeMB > 20)
                    {
                        GD.PrintErr($"AmiMain:OnUploadFileDialogSelected - Image too large: {fileSizeMB}MB > 20MB limit");
                        _outputLabel.Text += $"[code]AMI> Error: Image '{filename}' is {fileSizeMB}MB—exceeds 20MB limit. Resize and try again.[/code]";
                        return;
                    }

                    // Store for WritePrompt (no save to UploadedFiles)  
                    _appManager._pendingImageBytes = imageBytes;
                    _appManager._pendingImageFilename = filename;
                    _appManager._pendingImageMime = ext switch
                    {
                        ".jpg" or ".jpeg" => "image/jpeg",
                        ".png" => "image/png",
                        ".gif" => "image/gif",
                        ".webp" => "image/webp",
                        _ => "image/jpeg" // Default  
                    };

                    //GD.Print($"AmiMain:OnUploadFileDialogSelected - Processed image upload: {filename} ({fileSizeMB}MB, {_appManager._pendingImageMime})");
                    _outputLabel.Text += $"Image '{filename}' uploaded and ready for vision analysis.";
                }
                else
                {
                    GD.PrintErr($"Unsupported file type: {ext}");
                    _outputLabel.Text = $"[code]AMI> {ext} is not a supported file type.\nSupported file extensions are\n.txt, .json, .md, .csv\n.cc, .cpp, .cs, .py, .gd, .tscn\n.html, .css, .xaml\n.png, .jpg/jpeg, and .gif. [/code]";
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"AmiMain:OnUploadFileDialogSelected - Error processing upload: {e.Message}");
                _outputLabel.Text += $"Error processing '{System.IO.Path.GetFileName(path)}': {e.Message}";
            }
        }

        //Utility to rewrite _currentModelData
        public void UpdateModelData()
        {
            try
            {
                if (_modelsData != null && _modelsData.TryGetValue("models", out var dataObj) && dataObj is JsonElement dataElement && dataElement.ValueKind == JsonValueKind.Array)
                    foreach (var modelElement in dataElement.EnumerateArray())
                {
                    if (modelElement.TryGetProperty("id", out var nameElement))
                    {
                        string modelName = nameElement.GetString();
                        var prompttokenprice = modelElement.GetProperty("prompt_text_token_price").GetDecimal();
                        var cachedtokenprice = modelElement.GetProperty("cached_prompt_text_token_price").GetDecimal();
                        var completiontokenprice = modelElement.GetProperty("completion_text_token_price").GetDecimal();
                        //file.StoreString($"{modelName}: prompt price {prompttokenprice}, cached price {cachedtokenprice}, output price {completiontokenprice}\n");
                        //GD.Print($"AmiMain:UpdateModelData - Found model: {modelName}");

                        // Extract modalities for vision support
                        string supportsVision = "false";
                        if (modelElement.TryGetProperty("input_modalities", out var modalitiesElement) && modalitiesElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var modality in modalitiesElement.EnumerateArray())
                            {
                                if (modality.GetString() == "image")
                                {
                                    supportsVision = "true";
                                    break;
                                }
                            }
                        }


                        if (modelName == _appManager._config["ModelSelected"].ToString())
                        {
                            _currentModelData["model_name"] = modelName;
                            _currentModelData["prompt_token_price"] = prompttokenprice;
                            _currentModelData["cached_token_price"] = cachedtokenprice;
                            _currentModelData["output_token_price"] = completiontokenprice;
                            _currentModelData["supports_vision"] = supportsVision;
                            //GD.Print("AmiMain:OnHttpRequestCompleted - Current selected model found in models list: ", _currentModelData["model_name"].ToString(), _currentModelData["output_token_price"].ToString());

                        }

                    }
                    //.GD.Print("AmiMain:OnHttpRequestCompleted - Processed models list successfully");

                }

            }
            catch (Exception e)
            {
                GD.PrintErr("Model data update failed: ", e);
            }
        }
        public void DisplayResponse(Dictionary<string, object> result)
        {
            try
            {
                var themeIcons = _appManager.SetTheme(_preferencesButton.GetPopup().IsItemChecked(1));
                _amiLabel.Text = $"[left][img=50%]{themeIcons["grok_icon"]}[/img]  AMI for Grok, powered by  [img=50%]{themeIcons["xai_icon"]}[/img][/left]";
                //GD.Print("DisplayResponse: AmiLabel.Text set to ", _amiLabel.Text);

                string budgetString = result["budget"].ToString();

                _budgetLabel.Text = $"{result["budget"]:c4}";
                _responseLabel.Text = $"{result["response_spend"]:c4}";
                _sessionLabel.Text = $"{result["session_spend"]:c4}";
                _runningLabel.Text = $"{result["running_spend"]:c4}";
                if (budgetString.ToFloat() < _appManager._config["Threshold"].ToString().ToFloat())
                {
                    _budgetLabel.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.4f));
                    _budgetLabel.Text = $"{result["budget"]:c4}";
                    //GD.Print("AmiMain:DisplayResponse - Budget below threshold, set BudgetLabel color to red.");
                }
                else if (_budgetLabel.HasThemeColorOverride("font_color"))
                    _budgetLabel.RemoveThemeColorOverride("font_color");

                //Update ModelLabel for Theme

                // Update TokensLabel
                // contextTokens always = "total_tokens"/1000 + "K"
                // if any other value > 99999, divide by 1000 and append "K" to numeric return
                if (result.TryGetValue("total_tokens", out var totalTokens))
                {
                    var context = (int)result["total_tokens"] / 1000;

                    var contextString = context + "K";

                    var cachedTokens = (int)result["cached_tokens"];
                    string cachedString = $"{cachedTokens}";
                    if (cachedTokens > 99999)
                    {
                        cachedTokens /= 1000;
                        cachedString = cachedTokens + "K";
                    }

                    var promptTokens = (int)result["prompt_tokens"];
                    string promptString = $"{promptTokens}";
                    if (promptTokens > 99999)
                    {
                        promptTokens /= 1000;
                        promptString = promptTokens + "K";
                    }

                    int completionTokens = (int)result["output_tokens"];
                    string completionString = $"{completionTokens}";
                    if (completionTokens > 99999)
                    {
                        completionTokens /= 1000;
                        completionString = completionTokens + "K";
                    }

                    var reasoningTokens = (int)result["reasoning_tokens"];
                    string reasoningString = $"{reasoningTokens}";
                    if (completionTokens > 99999)
                    {
                        reasoningTokens /= 1000;
                        reasoningString = reasoningTokens + "K";
                    }

                    _tokensLabel.Text = $"Context: {contextString}   Tokens: {cachedString} [img=40%]{themeIcons["cached_icon"]}[/img]  {promptString} [img=50%]{themeIcons["prompt_icon"]}[/img] {completionString} [img=50%]{themeIcons["output_icon"]}[/img]  {reasoningString} [img=40%]{themeIcons["reasoning_icon"]}[/img]";
                }

                // Update ModelLabel
                if (result.TryGetValue("connected", out var connectedObj) && connectedObj is bool connected && connected)
                {
                    _modelName = _appManager._config.TryGetValue("ModelSelected", out var m) ? m.ToString() : "grok-4-fast-non-reasoning";
                    _modelLabel.Text = $"[img=40%]res://Assets/connect_icon.png[/img] {_modelName}";
                }
                else
                {
                    _modelLabel.Text = "[img=40%]res://Assets/disconnect_icon.png[/img] No Model Selected";
                }

                // Update OutputLabel
                if (result.TryGetValue("text", out var text))
                {
                    _outputLabel.Text = text.ToString();
                }
                else if (result.TryGetValue("error", out var error))
                {
                    _outputLabel.Text = $"Error: {error}";
                }
                //GD.Print("AmiMain:DisplayResponse - Updated UI labels.");
            }
            catch (Exception e)
            {
                GD.PrintErr($"AmiMain:DisplayResponse - Error: {e.Message}");
            }
        }
    }
}
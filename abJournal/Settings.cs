namespace abJournal.Properties {
    // このクラスでは設定クラスでの特定のイベントを処理することができます:
    //  SettingChanging イベントは、設定値が変更される前に発生します。
    //  PropertyChanged イベントは、設定値が変更された後に発生します。
    //  SettingsLoaded イベントは、設定値が読み込まれた後に発生します。
    //  SettingsSaving イベントは、設定値が保存される前に発生します。
    internal sealed partial class Settings {
        
        public Settings() {
            // // 設定の保存と変更のイベント ハンドラーを追加するには、以下の行のコメントを解除します:
            //
            // this.SettingChanging += this.SettingChangingEventHandler;
            //
            // this.SettingsSaving += this.SettingsSavingEventHandler;
            //
        }

        protected override void OnSettingsLoaded(object sender, System.Configuration.SettingsLoadedEventArgs e) {
            if(PenDashed == null) PenDashed = new bool[] { false, false, false, false, false, false, false, false };
            if(PenThickness == null) PenThickness = new double[] { 1.5, 1.5, 1.5, 1.5, 1.5, 1.5, 1.5, 1.5 };
            if(PenColor == null) {
                PenColor = new System.Windows.Media.Color[8];
                for(int i = 0 ; i < PenColor.Length ; ++i) PenColor[i] = System.Windows.Media.Colors.Black;
            }
            if(History == null) History = new System.Collections.Specialized.StringCollection();
            base.OnSettingsLoaded(sender, e);
        }

        private void SettingChangingEventHandler(object sender, System.Configuration.SettingChangingEventArgs e) {
            // SettingChangingEvent イベントを処理するコードをここに追加してください。
        }
        
        private void SettingsSavingEventHandler(object sender, System.ComponentModel.CancelEventArgs e) {
            // SettingsSaving イベントを処理するコードをここに追加してください。
        }


        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public bool[] PenDashed {
            get {  return (bool[]) this["PenDashed"]; }
            set { this["PenDashed"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public double[] PenThickness {
            get { return (double[]) this["PenThickness"]; }
            set { this["PenThickness"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public System.Windows.Media.Color[] PenColor {
            get { return (System.Windows.Media.Color[]) this["PenColor"]; }
            set { this["PenColor"] = value; }
        }
    }
}

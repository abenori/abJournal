using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using ProtoBuf;
using System.IO.Compression;

namespace abJournal {
    public class TempFile {
        public string FileName { get; private set; }
        public TempFile() {
            FileName = Path.GetTempFileName();
            TempFileNames.Add(FileName);
        }
        public TempFile(TempFile f) {
            FileName = f.FileName;
        }

        static List<string> TempFileNames = new List<string>();
        // アプリケーションの終了時にファイルを削除する．
        class TempFileFinalizer {
            ~TempFileFinalizer() {
                foreach(var d in TempFile.TempFileNames) {
                    File.Delete(d);
                }
            }
        }
        static TempFileFinalizer finalizer = new TempFileFinalizer();
    }

    // 「添付ファイル」の管理を行う．
    // new AttachedFileでファイルを作り，いらなくなったら破棄すればよい．
    //
    // 保存時に参照されているファイル一覧を作ることが目的になっているけど，参照を全て見ればいい気もしてきた．
    // まぁいいや．
    public class AttachedFile : IDisposable {
        struct FileData {
            public string FileName;
            public string OriginalFileName;
            public string Identifier;
        }
        FileData data;
        public string FileName { get { return data.FileName; } }// ファイル名（一時ファイル）
        public string OriginalFileName { get { return data.OriginalFileName; } }// 元々のファイル名．拡張子などはこちらを参照
        public string Identifier { get { return data.Identifier; } }// 識別子（保存読み込みをしても不変）

        AttachedFile(FileData d){
            data = new FileData();
            data.FileName = d.FileName;
            data.OriginalFileName = d.OriginalFileName;
            data.Identifier = d.Identifier;
        }

        public AttachedFile() {
            var tmp = Path.GetTempFileName();
            data = new FileData();
            data.FileName = tmp;
            data.OriginalFileName = "";
            data.Identifier = GetNewIdentifier();
            attachedFiles[data] = 1;
        }

        public AttachedFile(string path) {
            var tmp = Path.GetTempFileName();
            File.Copy(path, tmp, true);
            // 読み取り専用の場合解除しておく（後でFile.Deleteに失敗するため）．
            (new FileInfo(tmp)).Attributes = FileAttributes.Normal;
            data = new FileData();
            data.FileName = tmp;
            data.OriginalFileName = Path.GetFileName(path);
            data.Identifier = GetNewIdentifier();
            attachedFiles[data] = 1;
        }
        public AttachedFile(AttachedFile file) {
            data = new FileData();
            data.FileName = file.FileName;
            data.OriginalFileName = file.OriginalFileName;
            data.Identifier = file.Identifier;
            ++attachedFiles[data];
        }
        public static AttachedFile GetFileFromIdentifier(string id) {
            foreach(var d in attachedFiles) {
                if(d.Key.Identifier == id) {
                    ++attachedFiles[d.Key];
                    return new AttachedFile(d.Key);
                }
            }
            return null;
        }
        public void Dispose() {
            if(data.FileName == null) throw new ObjectDisposedException("AttachedFile");
            --attachedFiles[data];
            data.FileName = null;
            data.OriginalFileName = null;
            data.Identifier = null;
        }

        [ProtoContract]
        public struct SavingAttachedFile {
            [ProtoMember(1)]
            public string Identifier;
            [ProtoMember(2)]
            public string OriginalFileName;
        }
        public static List<SavingAttachedFile> Save(ZipArchive zip) {
            var rv = new List<SavingAttachedFile>();
            foreach(var f in attachedFiles) {
                if(f.Value > 0) {
                    zip.CreateEntryFromFile(f.Key.FileName, "attached\\" + f.Key.Identifier);
                    rv.Add(new SavingAttachedFile() { OriginalFileName = f.Key.OriginalFileName, Identifier = f.Key.Identifier });
                }
            }
            return rv;
        }
        public static void Open(ZipArchive zip, List<SavingAttachedFile> files) {
            foreach(var f in files) {
                var entry = zip.GetEntry("attached\\" + f.Identifier);
                var tmp = Path.GetTempFileName();
                File.Delete(tmp);
                entry.ExtractToFile(tmp);
                var data = new FileData();
                data.FileName = tmp; data.OriginalFileName = f.OriginalFileName; data.Identifier = f.Identifier;
                attachedFiles[data] = 0;
            }
        }

        static string GetNewIdentifier() {
            if(attachedFiles.Count == 0) return "0";
            return (attachedFiles.Select(d => {
                int r;
                if(Int32.TryParse(d.Key.Identifier, out r)) return r;
                else return 0;
            }).Max() + 1).ToString();
        }
        static Dictionary<FileData, int> attachedFiles = new Dictionary<FileData, int>();


        // アプリケーションの終了時にファイルを削除する．
        class AttachedFileFinalizer {
            ~AttachedFileFinalizer() {
                foreach(var d in AttachedFile.attachedFiles) {
                    try {
                        File.Delete(d.Key.FileName);
                    }
                    catch(UnauthorizedAccessException e) {
                        System.Diagnostics.Debug.WriteLine(e.Message);
                    }
                }
            }
        }
        static AttachedFileFinalizer finalizer = new AttachedFileFinalizer();
    }
}



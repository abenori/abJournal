abJournal
=========
(C)ABE Noriyuki <http://www.math.sci.hokudai.ac.jp/~abenori/>

## これは何
ノートをとるためのソフトです．劣化Windows Journalです．一応以下がWindows Journalよりもよいところと思っています．
* デジタイザペンで書き込み，タッチでスクロール．（よってデジタイザペンがないと使えない．）
* スクロールバーがない．（手があたって勝手にスクロールしてしまうことがない．）
* 保存データはProtocol Buffersで保存される．（独自仕様だが仕様が明確．.jntよりはサイズが大きめ．読み込み書き込み遅め．）
* 破線用のペンが使える．
* PDFへの変換機能を持っている．

逆にWindows Journalはできるのにこちらではできないことは山のようにあります．基本自分用です．

## 動作条件
* Windows 8.1以降
* .NET Framework 4.5以降

## 使い方
適当に使えると思います．自分が必要な機能しか無いため，あまり色々はできません．保存の時に拡張子をpdfにするとPDFに変換されます．

## 描画アルゴリズム
設定から選べますが，場合によっては無視されます．具体的には
* 選択状態ではStroke.GetGeometryで固定．
* Stroke.GetGeometryが選択されている状態では，破線は独自アルゴリズムになる．
* PDF生成時は独自アルゴリズムで，更に筆圧無視．
筆圧＋独自アルゴリズムはかなり遅いです．

## 謝辞
* PDFへの変換はPDFSharpを使っています．
  <http://pdfsharp.com/PDFsharp/>
* Protocol Buffersへの（デ）シリアル化はProtobuf-netを使っています．
  <http://code.google.com/p/protobuf-net/>
* Option解析にはNDesk.Optionsを使っています．
  <http://www.ndesk.org/Options>
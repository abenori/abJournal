abJournal
=========
(C)Abe Noriyuki <http://www.math.sci.hokudai.ac.jp/~abenori/>

## これは何
ノートをとるためのソフトです．劣化Windows Journalです．一応以下がWindows Journalよりもよいところと思っています．
* デジタイザペンで書き込み，タッチでスクロールとできる．（よってデジタイザペンがないと使えない．）
* スクロールバーがない．（手があたって勝手にスクロールしてしまうことがない．）
* 保存データはProtocol Buffersで保存される．（仕様が明確．.jntよりはサイズが大きめ．）
* 破線用のペンが使える．

逆にWindows Journalはできるのにこちらではできないことは山のようにあります．基本自分用です．

## 動作条件
* Windows 8.1以降
* .NET Framework 4.5以降

## 使い方
適当に使えると思います．自分が必要な機能しか無いため，あまり色々はできません．保存の時に拡張子をpdfにするとPDFに変換されます．

## 謝辞
* PDFへの変換はPDFSharpを使っています．
  <http://pdfsharp.com/PDFsharp/>
* Protocol Bufferへの（デ）シリアル化はProtobuf-netを使っています．
  <http://code.google.com/p/protobuf-net/>
* Option解析にはNDesk.Optionsを使おうとしています．（現在オプション無し．）
  <http://www.ndesk.org/Options>


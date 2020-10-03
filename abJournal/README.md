abJournal
=========
(C)ABE Noriyuki <http://www.math.sci.hokudai.ac.jp/~abenori/>

## これは何
ノートをとるためのソフトです．劣化Windows Journalです．一応以下がWindows Journalよりもよいところと思っています．
* ペンによる書き込みとタッチでのスクロールを分けて認識．（デジタイザペン，マウスはペンでの書き込みと認識．タッチはその面積が小さければペンと判断．タッチ判断は試していないのでうまく行くか不明．）
* スクロールバーがない．（手があたって勝手にスクロールしてしまうことがない．）
* 保存データはProtocol Buffersで保存される．（.jntよりはサイズが大きめ．読み込み書き込み遅め．）
* 破線用のペンが使える．

以下も一応Windows Journalではできないところですが，他のソフトウェアなどを併用すれば可能なので，個人的にはあまり利点にはなっていません．
* PDFへの変換機能を持っている．
* XPS/PDFを他のソフトを使わずにインポートできる．

逆にWindows Journalはできるのにこちらではできないことは山のようにあります．基本自分用です．

## 動作条件
* Windows 8.1以降
* .NET Framework 4.5以降
* PDF作成時に文字を認識させる場合は，IACore.dll / IALoader.dll / IAWinFX.dll  （Windows SDK for Vistaに入っています）

## 使い方
適当に使えると思います．自分が必要な機能しか無いため，あまり色々はできません．

## PDF変換
保存の時に拡張子をpdfにするとPDFに変換されます．背景はもともとがPDFのもの以外無視されます．
文字列を認識し，透明テキストとして書き込みます．変換には時間がかかります．また追加でファイルが必要です．

## 保存ファイル
zipでアーカイブされていて，解凍すると
* _data.abjnt
* attached/*

がでてきます．_data.abjntがprotobufでシリアライズされたデータ本体．（abInkCanvasManager.ablibInkCanvasCollectionSavingProtobufData2型．添付しているabJournal.protoも参照．このファイルはabJournal.exe /getprotoschemaでも生成されます．）
attachedの中が，このファイルに付随するファイルたちです（たとえば背景に使われている画像とか）．

## 謝辞
* PDFへの変換はiTextSharpを使っています．
  <http://sourceforge.net/projects/itextsharp/>
* PDFの描画はPDFiumを使っています．
  <https://pdfium.googlesource.com/pdfium/>
* Protocol Buffersへの（デ）シリアル化はProtobuf-netを使っています．
  <https://github.com/mgravell/protobuf-net>
* Option解析にはNDesk.Optionsを使っています．
  <http://www.ndesk.org/Options>

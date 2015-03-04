abJournal
=========
(C)ABE Noriyuki <http://www.math.sci.hokudai.ac.jp/~abenori/>

## これは何
ノートをとるためのソフトです．劣化Windows Journalです．一応以下がWindows Journalよりもよいところと思っています．
* ペンによる書き込みとタッチでのスクロールを分けて認識．（デジタイザペン，マウスはペンでの書き込みと認識．タッチはその面積が小さければペンと判断．タッチ判断は試していない．）
* スクロールバーがない．（手があたって勝手にスクロールしてしまうことがない．）
* 保存データはProtocol Buffersで保存される．（.jntよりはサイズが大きめ．読み込み書き込み遅め．）
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

速度はだいたい

線を引くだけ > 独自アルゴリズム > Strokg.GetGeometry > Strokg.GetGeometry + 筆圧 > 独自アルゴリズム + 筆圧

という感じです．特に独自アルゴリズム + 筆圧はかなり遅いです．線を引くだけ + 筆圧がどこに入るかはよくわからない，

## 保存ファイル
zipでアーカイブされていて，解凍すると
* _data.abjnt
* attached\*
がでてきます．_data.abjntがprotobufでシリアライズされたデータ本体（abInkCanvasManager.ablibInkCanvasCollectionSavingProtobufData型）
attachedの中が，このファイルに付随するファイルたちです（たとえば背景に使われている画像とか）．

## 謝辞
* PDFへの変換はPDFSharpを使っています．
  <http://www.pdfsharp.net/>
* Protocol Buffersへの（デ）シリアル化はProtobuf-netを使っています．
  <http://code.google.com/p/protobuf-net/>
* Option解析にはNDesk.Optionsを使っています．
  <http://www.ndesk.org/Options>
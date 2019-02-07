// MainWindow.cs - CiViewer main window
//
// Copyright (C) 2013-2019  Enrico Croce
//
// This file is part of CiTo, see http://cito.sourceforge.net
//
// CiTo is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// CiTo is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with CiTo.  If not, see http://www.gnu.org/licenses/

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Foxoft.Ci;
using Gtk;
using Pango;

public partial class MainWindow : Gtk.Window {
  protected ProjectFiles Project = new ProjectFiles();
  //
  private static Gdk.Atom _atom = Gdk.Atom.Intern("CLIPBOARD", false);
  private Gtk.Clipboard _clipBoard = Gtk.Clipboard.Get(_atom);
  private bool InUpdate = false;
  private static Gtk.TargetEntry[] target_table = new TargetEntry[] { new TargetEntry("text/uri-list", 0, 0) };

  public MainWindow() : base(Gtk.WindowType.Toplevel) {
    Build();
    InUpdate = true;
    tvSource.Buffer.Changed += OnSourceChange;
    iNameSpace.Changed += OnSourceChange;
    SetMessage(null, null);
    PopulateCombo(cbSource, Project.GetSources());
    PopulateCombo(cbLanguage, GeneratorHelper.GetLanguages());
    FixMac();
    Pango.FontDescription font = Pango.FontDescription.FromString("monospace 12");
    tvSource.ModifyFont(font);
    tvTarget.ModifyFont(font);
    SetTabs(tvSource, font, 2);
    SetTabs(tvTarget, font, 2);
    InUpdate = false;
    TranslateCode();
    Gtk.Drag.DestSet(tvSource, DestDefaults.All, target_table, Gdk.DragAction.Copy);
    tvSource.DragDataReceived += Data_Received;
  }

  void Data_Received(object o, DragDataReceivedArgs args) {
    string data = System.Text.Encoding.UTF8.GetString(args.SelectionData.Data);
    List<string> paths = new List<string>(Regex.Split(data, "\r\n"));
    paths.RemoveAll(string.IsNullOrEmpty);
    LoadSourceFiles(paths.ToArray());
    Gtk.Drag.Finish(args.Context, true, true, args.Time);
  }

  private CodePosition OldPos = null;

  private void SetMessage(string msg, CodePosition pos) {
    OldPos = pos;
    if (String.IsNullOrEmpty(msg)) {
      lbMsg.Text = "";
      btLocate.Visible = false;
    }
    else {
      StringBuilder txt = new StringBuilder(msg);
      lbMsg.Text = txt.ToString();
      btLocate.Visible = (pos != null);
      OldPos = pos;
      if (pos != null) {
        txt.AppendFormat(" @{0}:{1}", pos.SourceFilename, pos.Offset);
      }
      lbMsg.Text = txt.ToString();
    }
  }

  protected void btLocateClick(object sender, EventArgs e) {
    bool found = true;
    if (!cbSource.ActiveText.Equals(OldPos.SourceFilename)) {
      Gtk.TreeIter iter;
      cbSource.Model.GetIterFirst(out iter);
      found = false;
      do {
        GLib.Value thisRow = new GLib.Value();
        cbSource.Model.GetValue(iter, 0, ref thisRow);
        if ((thisRow.Val as string).Equals(OldPos.SourceFilename)) {
          cbSource.SetActiveIter(iter);
          found = true;
          break;
        }
      }
      while (cbSource.Model.IterNext(ref iter));
    }
    if (found) {
      TextIter p = tvSource.Buffer.GetIterAtOffset(OldPos.Offset);
      tvSource.Buffer.PlaceCursor(p);
      tvSource.ScrollToIter(p, 0, true, 0.5, 0.5);
      tvSource.HasFocus = true;
    }
  }

  private void SetTabs(TextView textview, Pango.FontDescription font, int numSpaces) {
    int charWidth = 0;
    int charHeight = 0;
    var layout = textview.CreatePangoLayout("A");
    var tabs = new TabArray(30, true);
    layout.FontDescription = font;
    layout.GetPixelSize(out charWidth, out charHeight);
    for (int i = 0; i < tabs.Size; i++) {
      tabs.SetTab(i, TabAlign.Left, i * charWidth * numSpaces);
    }
    textview.Tabs = tabs;
  }

  private void PopulateCombo(ComboBox cb, string[] items) {
    cb.Clear();
    ListStore store = new ListStore(typeof(string));
    foreach (var item in items) {
      store.AppendValues(item);
    }
    cb.Model = store;
    var cellRenderer = new CellRendererText();
    cb.PackStart(cellRenderer, true);
    cb.AddAttribute(cellRenderer, "text", 0);
    cb.Active = 0;
  }

  private void TranslateCode() {
    SetMessage(null, null);
    tvTarget.Buffer.Text = "";
    try {
      Project.GenerateTarget(cbLanguage.ActiveText, iNameSpace.Text);
    }
    catch (CiCodeException e) {
      SetMessage(e.Message, e.Position);
    }
    catch (Exception e) {
      Console.WriteLine(e.StackTrace);
      lbMsg.Text = e.Message;
    }
    PopulateCombo(cbTarget, Project.GetTargets());
  }

  private void LoadSourceFiles(string[] Filenames) {
    InUpdate = true;
    Project.LoadFiles(Filenames);
    PopulateCombo(cbSource, Project.GetSources());
    InUpdate = false;
    TranslateCode();
  }

  protected void OnDeleteEvent(object sender, DeleteEventArgs a) {
    Application.Quit();
    a.RetVal = true;
  }

  protected void OnExit(object sender, EventArgs e) {
    Application.Quit();
  }

  protected void OnOpen(object sender, EventArgs e) {
    FileChooserDialog chooser = new FileChooserDialog("Please select a Ć file to view ...", this, FileChooserAction.Open, "Cancel", ResponseType.Cancel, "Open", ResponseType.Accept);
    FileFilter filter_1 = new FileFilter();
    filter_1.Name = "Ć files";
    filter_1.AddPattern("*.ci");
    FileFilter filter_2 = new FileFilter();
    filter_2.Name = "All files";
    filter_2.AddPattern("*");
    chooser.AddFilter(filter_1);
    chooser.AddFilter(filter_2);
    chooser.SelectMultiple = true;
    if (chooser.Run() == (int)ResponseType.Accept) {
      lbMsg.Text = "File(s) loaded.";
      LoadSourceFiles(chooser.Filenames);
    }
    chooser.Destroy();
  }

  protected void OnFontItem(object sender, EventArgs e) {
    lbMsg.Text = "";
    FontSelectionDialog fdia = new FontSelectionDialog("Select font name");
    fdia.Response += delegate (object o, ResponseArgs resp) {
      if (resp.ResponseId == ResponseType.Ok) {
        Pango.FontDescription fontdesc = Pango.FontDescription.FromString(fdia.FontName);
        tvSource.ModifyFont(fontdesc);
        tvTarget.ModifyFont(fontdesc);
      }
    };
    fdia.Run();
    fdia.Destroy();
  }

  protected ProjectFile GetFileInfo(string key, Dictionary<string, ProjectFile> db) {
    ProjectFile file = null;
    if (key != null) {
      db.TryGetValue(key, out file);
    }
    return file;
  }

  protected void cbSourceChanged(object sender, EventArgs e) {
    ProjectFile file = GetFileInfo(cbSource.ActiveText, Project.Source);
    if (file != null) {
      tvSource.Buffer.Text = file.Code;
    }
  }

  protected void cbTargetChanged(object sender, EventArgs e) {
    ProjectFile file = GetFileInfo(cbTarget.ActiveText, Project.Target);
    if (file != null) {
      tvTarget.Buffer.Text = file.Code;
    }
  }

  protected void OnSourceChange(object sender, EventArgs e) {
    if (AutoTranslateAction.Active && !InUpdate) {
      CopySource();
      TranslateCode();
    }
  }

  protected void CopySource() {
    if (tvSource.Buffer.Modified) {
      ProjectFile file = GetFileInfo(cbSource.ActiveText, Project.Source);
      if (file != null) {
        tvSource.Buffer.Modified = false;
        file.Code = tvSource.Buffer.Text;
        file.Changed = true;
      }
    }
  }

  protected void OnTranslate(object sender, EventArgs e) {
    CopySource();
    TranslateCode();
  }

  protected void SaveCode(Dictionary<string, ProjectFile> db) {
    foreach (ProjectFile file in db.Values) {
      if (file.Changed) {
        System.IO.File.WriteAllText(file.Path, file.Code);
        file.Changed = false;
      }
    }
  }

  protected void OnSaveSource(object sender, EventArgs e) {
    try {
      SaveCode(Project.Source);
      lbMsg.Text = "Source saved";
    }
    catch (Exception ex) {
      lbMsg.Text = ex.Message;
    }
  }

  protected void OnSaveTarget(object sender, EventArgs e) {
    try {
      SaveCode(Project.Target);
      lbMsg.Text = "Target saved";
    }
    catch (Exception ex) {
      lbMsg.Text = ex.Message;
    }
  }

  protected void OnLanguageChange(object sender, EventArgs e) {
    if (!InUpdate) {
      TranslateCode();
    }
  }

  private void FixMac() {
  }

  protected void OnCopyTargetToClipboardActionActivated(object sender, EventArgs e) {
    if (String.IsNullOrEmpty(lbMsg.Text)) {
      _clipBoard.Text = tvTarget.Buffer.Text;
    }
    else {
      _clipBoard.Text = lbMsg.Text;
    }
  }
}

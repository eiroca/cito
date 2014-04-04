// Generator.cs - base class for code generators
//
// Copyright (C) 2013  Enrico Croce
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
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace Foxoft.Ci {

  public delegate TextWriter TextWriterFactory(string filename);
  //
  public class TextWriterFileFactory {
    static public TextWriter Make(string filename) {
      return File.CreateText(filename);
    }
  }

  public interface IGenerator {
    void SetTextWriterFactory(TextWriterFactory aFactory);

    void SetOutputFile(string aOutputFile);

    void SetNamespace(string aNamespace);

    void SetOption(string option, object value);

    object GetOption(string option, object def);

    void WriteProgram(CiProgram program);
  }

  public abstract class BaseGenerator : IGenerator {
    protected static string ToCamelCase(string s) {
      return char.ToLowerInvariant(s[0]) + s.Substring(1);
    }

    protected static string ToCapitalize(string s) {
      return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    protected static string ToLowercase(string s) {
      StringBuilder res = new StringBuilder(s.Length);
      foreach (char c in s) {
        res.Append(char.ToLowerInvariant(c));
      }
      return res.ToString();
    }

    protected static string ToUppercaseWithUnderscores(string s) {
      StringBuilder res = new StringBuilder(s.Length);
      bool first = true;
      foreach (char c in s) {
        if (char.IsUpper(c) && !first) {
          res.Append('_');
          res.Append(c);
        }
        else {
          res.Append(char.ToUpperInvariant(c));
        }
        first = false;
      }
      return res.ToString();
    }

    protected static string ToLowercaseWithUnderscores(string s) {
      StringBuilder res = new StringBuilder(s.Length);
      bool first = true;
      foreach (char c in s) {
        if (char.IsUpper(c)) {
          if (!first) {
            res.Append('_');
          }
          res.Append(char.ToLowerInvariant(c));
        }
        else {
          res.Append(c);
        }
        first = false;
      }
      return res.ToString();
    }

    public string OutputFile {
      get;
      set;
    }

    public string Namespace {
      get;
      set;
    }

    public Dictionary<string, object> Options = new Dictionary<string, object>();

    public TextWriterFactory CreateTextWriter {
      get;
      set;
    }

    public BaseGenerator(string aNamespace) : this() {
      SetNamespace(aNamespace);
    }

    public BaseGenerator() {
      CreateTextWriter = TextWriterFileFactory.Make;
    }

    #region IGenerator
    public virtual void SetTextWriterFactory(TextWriterFactory aFactory) {
      CreateTextWriter = aFactory;
    }

    public virtual void SetOutputFile(string aOutputFile) {
      this.OutputFile = aOutputFile;
    }

    public virtual void SetNamespace(string aNamespace) {
      this.Namespace = aNamespace;
    }

    public virtual void SetOption(string option, object value) {
      if (!Options.ContainsKey(option)) {
        Options.Add(option, value);
      }
      else {
        Options[option] = value;
      }
    }

    public virtual object GetOption(string option, object def) {
      object result = def;
      Options.TryGetValue(option, out result);
      return result;
    }

    protected CiProgram CurrentProgram;

    public virtual void WriteProgram(CiProgram program) {
      this.CurrentProgram = program;
    }
    #endregion

    #region TextWriterFactory
    protected virtual void CreateFile(string filename) {
      Indent = 0;
      Open(CreateTextWriter(filename));
      WriteBanner();
    }

    protected virtual void CloseFile() {
      WriteFooter();
      Close();
    }

    protected virtual void WriteBanner() {
      WriteComment(" Generated automatically with \"cito\". Do not edit.");
      if (CurrentProgram != null) {
        WriteComment(CurrentProgram.GlobalComment);
      }
    }

    protected virtual void WriteFooter() {
    }
    #endregion

    #region LowLevelWrite
    protected string IndentStr = "  ";
    protected string NewLineStr = "\r\n";
    protected TextWriter Writer;
    //
    //
    protected StringBuilder curLine = null;
    protected StringBuilder fullCode = null;
    protected int Position = 0;

    protected virtual void Open(TextWriter writer) {
      this.Writer = writer;
      this.Indent = 0;
      this.Position = 0;
      curLine = new StringBuilder();
      fullCode = new StringBuilder();
    }

    protected virtual void Flush() {
      string newTxt = curLine.ToString().TrimEnd();
      if (newTxt.Length > 0) {
        int oldLength = fullCode.Length;
        AppendIndentStr(fullCode);
        Position += (fullCode.Length - oldLength);  
        oldLength = fullCode.Length;
        fullCode.Insert(Position, newTxt);
        Position += (fullCode.Length - oldLength);  
      }
      curLine = new StringBuilder();
    }

    protected virtual void Close() {
      Flush();
      Writer.Write(fullCode);
      Writer.Close();
    }

    protected virtual void Write(char c) {
      curLine.Append(c);
    }

    protected virtual void Write(int i) {
      curLine.Append(i);
    }

    protected virtual void Write(string s) {
      curLine.Append(s);
    }

    protected virtual void WriteFormat(string format, params object[] args) {
      curLine.AppendFormat(format, args);
    }

    protected virtual void WriteLine(string s) {
      Write(s);
      WriteLine();
    }

    protected virtual void WriteLine(string format, params object[] args) {
      WriteFormat(format, args);
      WriteLine();
    }

    protected virtual void WriteLine() {
      Flush();
      fullCode.Insert(Position, NewLineStr);
      Position += NewLineStr.Length;
    }

    protected virtual void AppendIndentStr(StringBuilder res) {
      int len = this.IndentStr.Length;
      for (int i = 0; i < this.Indent; i++) {
        res.Append(IndentStr);
      }
    }

    public class PositionMark {
      private int FPos;

      public int Position {
        get {
          return FPos;
        }
      }

      public PositionMark(int Position) {
        FPos = Position;
      }
    }

    public PositionMark GetMark() {
      Flush();
      return new PositionMark(Position);
    }

    /**
     * @param mark if null goto end of buffer
     */
    public void SetMark(PositionMark mark) {
      Flush();
      if (mark == null) {
        Position = fullCode.Length;
      }
      else {
        Position = mark.Position;
      }
    }
    #endregion

    #region Blocks
    private int Indent = 0;
    protected string BlockOpenStr = "{";
    protected string BlockCloseStr = "}";
    protected bool BlockOpenCR = true;
    protected bool BlockCloseCR = true;

    protected  bool IsIndented() {
      return (Indent != 0);
    }

    protected void OpenBlock() {
      OpenBlock(true);
    }

    protected void CloseBlock() {
      CloseBlock(true);
    }

    protected virtual void OpenBlock(bool explict) {
      if (explict) {
        Write(BlockOpenStr);
        if (BlockOpenCR) {
          WriteLine();
        }
      }
      this.Indent++;
    }

    protected virtual void CloseBlock(bool explict) {
      this.Indent--;
      if (explict) {
        Write(BlockCloseStr);
        if (BlockCloseCR) {
          WriteLine();
        }
      }
    }

    protected string SimpleCommentFormat = "//{0}";

    public void WriteComment(string comment) {
      WriteLine(SimpleCommentFormat, comment ?? "");
    }

    public void WriteComment(CiComment comments) {
      if (comments != null) {
        foreach (string comment in comments.Comments) {
          WriteLine(SimpleCommentFormat, comment ?? "");
        }
      }
    }
    #endregion
  }
}

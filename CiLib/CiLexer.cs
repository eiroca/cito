// CiLexer.cs - Ci lexer
//
// Copyright (C) 2011-2013  Piotr Fusik
// Copyright (C) 2014  Enrico Croce
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
using System.IO;
using System.Text;

namespace Foxoft.Ci {

  public enum CiToken {
    EndOfFile,
    Id,
    IntConstant,
    StringConstant,
    Semicolon,
    Dot,
    Comma,
    LeftParenthesis,
    RightParenthesis,
    LeftBracket,
    RightBracket,
    LeftBrace,
    RightBrace,
    Plus,
    Minus,
    Asterisk,
    Slash,
    Mod,
    And,
    Or,
    Xor,
    Not,
    ShiftLeft,
    ShiftRight,
    Equal,
    NotEqual,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
    CondAnd,
    CondOr,
    CondNot,
    Assign,
    AddAssign,
    SubAssign,
    MulAssign,
    DivAssign,
    ModAssign,
    AndAssign,
    OrAssign,
    XorAssign,
    ShiftLeftAssign,
    ShiftRightAssign,
    Increment,
    Decrement,
    QuestionMark,
    Colon,
    DocComment,
    PasteTokens,
    Abstract,
    Break,
    Case,
    Class,
    Const,
    Continue,
    Default,
    Delegate,
    Delete,
    Do,
    Else,
    Enum,
    For,
    Goto,
    If,
    Internal,
    Macro,
    Native,
    New,
    Override,
    Public,
    Return,
    Static,
    Switch,
    Throw,
    Virtual,
    Void,
    While,
    EndOfLine,
    PreIf,
    PreElIf,
    PreElse,
    PreEndIf,
    Comment
  }

  public class CodePosition {
    public string SourceFilename;
    public int Offset;
  }

  [Serializable]
  public class CiCodeException : Exception {
    public readonly CodePosition Position;

    public CiCodeException(CodePosition position, string message) : base(message) {
      this.Position = position;
    }
  }

  [Serializable]
  public class ParseException : CiCodeException {
    public ParseException(CodePosition position, string message) : base(position, message) {
    }

    public ParseException(CodePosition position, string format, params object[] args) : this(position, string.Format(format, args)) {
    }
  }

  public class CiLexer {
    private const char SPECIAL_TAB = '\t';
    private const char SPECIAL_LF = '\r';
    public const char SPECIAL_SPACE = ' ';
    public const char SPECIAL_CR = '\n';
    TextReader Reader;
    protected string SourceFilename;
    public int InputLineNo;
    public int Position;
    protected CiToken CurrentToken;
    protected string CurrentString;
    protected int CurrentInt;
    protected StringBuilder CopyTo;
    public HashSet<string> PreSymbols;
    bool AtLineStart = true;
    bool LineMode = false;

    public CiLexer() {
      this.PreSymbols = new HashSet<string>();
      this.PreSymbols.Add("true");
    }

    public CodePosition Here() {
      CodePosition result = new CodePosition();
      result.Offset = Position;
      result.SourceFilename = SourceFilename;
      return result;
    }

    protected void Open(string sourceFilename, TextReader reader) {
      this.SourceFilename = sourceFilename;
      this.Reader = reader;
      this.InputLineNo = 1;
      this.Position = 0;
      NextToken();
    }

    protected virtual bool IsExpandingMacro {
      get {
        return false;
      }
    }

    protected virtual bool ExpandMacroArg(string name) {
      return false;
    }

    protected virtual bool OnStreamEnd() {
      return false;
    }

    protected TextReader SetReader(TextReader reader) {
      TextReader old = this.Reader;
      this.Reader = reader;
      return old;
    }

    public static bool IsLetter(int c) {
      bool result = ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || (c == '_'));
      return result;
    }

    StringReader IdReader = null;

    public int PeekChar() {
      int c;
      if (this.IdReader != null) {
        c = this.IdReader.Peek();
      }
      else {
        c = Peek();
        if (c == SPECIAL_TAB) {
          c = SPECIAL_SPACE;
        }
        else if (c == SPECIAL_LF) {
          c = SPECIAL_CR;
        }
      }
      return c;
    }

    private int Peek() {
      int c = this.Reader.Peek();
      return c;
    }

    private int Read() {
      int c = this.Reader.Read();
      if (!this.IsExpandingMacro) {
        Position++;
      }
      return c;
    }

    public int ReadChar() {
      int c;
      if (this.IdReader != null) {
        c = this.IdReader.Read();
        if (this.IdReader.Peek() < 0) {
          this.IdReader = null;
        }
      }
      else {
        c = Read();
        if (IsLetter(c)) {
          StringBuilder sb = new StringBuilder();
          for (;;) {
            sb.Append((char)c);
            c = Peek();
            if (!IsLetter(c)) {
              break;
            }
            Read();
          }
          if (c == '#' && this.IsExpandingMacro) {
            Read();
            if (Read() != '#') {
              throw new ParseException(Here(), "Invalid character");
            }
          }
          string s = sb.ToString();
          if (!ExpandMacroArg(s)) {
            this.IdReader = new StringReader(s);
          }
          return ReadChar();
        }
        int nxt;
        switch (c) {
          case SPECIAL_TAB:
            c = SPECIAL_SPACE;
            break;
          case SPECIAL_LF: 
            nxt = Peek();
            if (nxt == SPECIAL_CR) {
              Read();
            }
            c = SPECIAL_CR;
            break;
          case SPECIAL_CR: 
            nxt = Peek();
            if (nxt == SPECIAL_LF) {
              Read();
            }
            break;
        }
        if (c == SPECIAL_CR && !this.IsExpandingMacro) {
          this.InputLineNo++;
          this.AtLineStart = true;
        }
      }
      if (c >= 0) {
        if (this.CopyTo != null) {
          this.CopyTo.Append((char)c);
        }
        switch (c) {
          case SPECIAL_SPACE:
          case SPECIAL_CR:
            break;
          default:
            this.AtLineStart = false;
            break;
        }
        while (Peek() < 0 && OnStreamEnd()) ;
      }
      return c;
    }

    void Skip(int c) {
      while (PeekChar() == c) {
        ReadChar();
      }
    }

    bool EatChar(int c) {
      if (PeekChar() == c) {
        ReadChar();
        return true;
      }
      return false;
    }

    int ReadDigit(bool hex) {
      int c = PeekChar();
      if (c >= '0' && c <= '9') return ReadChar() - '0';
      if (hex) {
        if (c >= 'a' && c <= 'f') return ReadChar() - 'a' + 10;
        if (c >= 'A' && c <= 'F') return ReadChar() - 'A' + 10;
      }
      return -1;
    }

    char ReadCharLiteral() {
      int c = ReadChar();
      if (c < 32) throw new ParseException(Here(), "Invalid character in literal");
      if (c != '\\') return (char)c;
      switch (ReadChar()) {
        case 't':
          return SPECIAL_TAB;
        case 'r':
          return SPECIAL_LF;
        case 'n':
          return SPECIAL_CR;
        case '\\':
          return '\\';
        case '\'':
          return '\'';
        case '"':
          return '"';
        default:
          throw new ParseException(Here(), "Unknown escape sequence");
      }
    }

    string ReadId(int c) {
      StringBuilder sb = new StringBuilder();
      for (;;) {
        sb.Append((char)c);
        if (!IsLetter(PeekChar())) break;
        c = ReadChar();
      }
      return sb.ToString();
    }

    void ReadHexNumber() {
      int i = ReadDigit(true);
      if (i < 0) {
        throw new ParseException(Here(), "Invalid hex number");
      }
      for (;;) {
        int d = ReadDigit(true);
        if (d < 0) {
          this.CurrentInt = i;
          return;
        }
        if (i > 0x7ffffff) {
          throw new ParseException(Here(), "Hex number too big");
        }
        i = (i << 4) + d;
      }
    }

    CiToken ReadPreToken() {
      for (;;) {
        bool atLineStart = this.AtLineStart;
        int c = ReadChar();
        switch (c) {
          case -1:
            return CiToken.EndOfFile;
          case SPECIAL_SPACE:
            continue;
          case SPECIAL_CR:
            if (this.LineMode) {
              return CiToken.EndOfLine;
            }
            continue;
          case '#':
            c = ReadChar();
            if (c == '#') return CiToken.PasteTokens;
            if (atLineStart && IsLetter(c)) {
              string s = ReadId(c);
              switch (s) {
                case "if":
                  return CiToken.PreIf;
                case "elif":
                  return CiToken.PreElIf;
                case "else":
                  return CiToken.PreElse;
                case "endif":
                  return CiToken.PreEndIf;
                default:
                  throw new ParseException(Here(), "Unknown preprocessor directive #" + s);
              }
            }
            throw new ParseException(Here(), "Invalid character");
          case ';':
            return CiToken.Semicolon;
          case '.':
            return CiToken.Dot;
          case ',':
            return CiToken.Comma;
          case '(':
            return CiToken.LeftParenthesis;
          case ')':
            return CiToken.RightParenthesis;
          case '[':
            return CiToken.LeftBracket;
          case ']':
            return CiToken.RightBracket;
          case '{':
            return CiToken.LeftBrace;
          case '}':
            return CiToken.RightBrace;
          case '+':
            if (EatChar('+')) return CiToken.Increment;
            if (EatChar('=')) return CiToken.AddAssign;
            return CiToken.Plus;
          case '-':
            if (EatChar('-')) return CiToken.Decrement;
            if (EatChar('=')) return CiToken.SubAssign;
            return CiToken.Minus;
          case '*':
            if (EatChar('=')) return CiToken.MulAssign;
            return CiToken.Asterisk;
          case '/':
            if (EatChar('/')) {
              c = PeekChar();
              if (c == '/') {
                ReadChar();
                Skip(SPECIAL_SPACE);
                return CiToken.DocComment;
              }
              StringBuilder sb = new StringBuilder();
              c = PeekChar();
              while (c != SPECIAL_CR && c >= 0) {
                sb.Append((char)c);
                ReadChar();
                c = PeekChar();
              }
              this.CurrentString = sb.ToString();
              return CiToken.Comment;
            }
            if (EatChar('=')) return CiToken.DivAssign;
            return CiToken.Slash;
          case '%':
            if (EatChar('=')) return CiToken.ModAssign;
            return CiToken.Mod;
          case '&':
            if (EatChar('&')) return CiToken.CondAnd;
            if (EatChar('=')) return CiToken.AndAssign;
            return CiToken.And;
          case '|':
            if (EatChar('|')) return CiToken.CondOr;
            if (EatChar('=')) return CiToken.OrAssign;
            return CiToken.Or;
          case '^':
            if (EatChar('=')) return CiToken.XorAssign;
            return CiToken.Xor;
          case '=':
            if (EatChar('=')) return CiToken.Equal;
            return CiToken.Assign;
          case '!':
            if (EatChar('=')) return CiToken.NotEqual;
            return CiToken.CondNot;
          case '<':
            if (EatChar('<')) {
              if (EatChar('=')) return CiToken.ShiftLeftAssign;
              return CiToken.ShiftLeft;
            }
            if (EatChar('=')) return CiToken.LessOrEqual;
            return CiToken.Less;
          case '>':
            if (EatChar('>')) {
              if (EatChar('=')) return CiToken.ShiftRightAssign;
              return CiToken.ShiftRight;
            }
            if (EatChar('=')) return CiToken.GreaterOrEqual;
            return CiToken.Greater;
          case '~':
            return CiToken.Not;
          case '?':
            return CiToken.QuestionMark;
          case ':':
            return CiToken.Colon;
          case '\'':
            this.CurrentInt = ReadCharLiteral();
            if (ReadChar() != '\'') {
              throw new ParseException(Here(), "Unterminated character literal");
            }
            return CiToken.IntConstant;
          case '"':
            {
              StringBuilder sb = new StringBuilder();
              while (PeekChar() != '"') sb.Append(ReadCharLiteral());
              ReadChar();
              this.CurrentString = sb.ToString();
              return CiToken.StringConstant;
            }
          case '$':
            ReadHexNumber();
            return CiToken.IntConstant;
          case '0':
            if (EatChar('x')) {
              ReadHexNumber();
              return CiToken.IntConstant;
            }
            goto case '1';
          case '1':
          case '2':
          case '3':
          case '4':
          case '5':
          case '6':
          case '7':
          case '8':
          case '9':
            {
              int i = c - '0';
              int numBase;
              if (i == 0) {
                numBase = 8;
              }
              else {
                numBase = 10;
              }
              for (;;) {
                int d = ReadDigit(false);
                if (d < 0) {
                  this.CurrentInt = i;
                  return CiToken.IntConstant;
                }
                if (d >= numBase) {
                  throw new ParseException(Here(), "Invalid octal number");
                }
                if (i > 214748364) {
                  throw new ParseException(Here(), "Integer too big");
                }
                i = numBase * i + d;
                if (i < 0) {
                  throw new ParseException(Here(), "Integer too big");
                }
              }
            }
          case 'A':
          case 'B':
          case 'C':
          case 'D':
          case 'E':
          case 'F':
          case 'G':
          case 'H':
          case 'I':
          case 'J':
          case 'K':
          case 'L':
          case 'M':
          case 'N':
          case 'O':
          case 'P':
          case 'Q':
          case 'R':
          case 'S':
          case 'T':
          case 'U':
          case 'V':
          case 'W':
          case 'X':
          case 'Y':
          case 'Z':
          case '_':
          case 'a':
          case 'b':
          case 'c':
          case 'd':
          case 'e':
          case 'f':
          case 'g':
          case 'h':
          case 'i':
          case 'j':
          case 'k':
          case 'l':
          case 'm':
          case 'n':
          case 'o':
          case 'p':
          case 'q':
          case 'r':
          case 's':
          case 't':
          case 'u':
          case 'v':
          case 'w':
          case 'x':
          case 'y':
          case 'z':
            {
              string s = ReadId(c);
              switch (s) {
                case "abstract":
                  return CiToken.Abstract;
                case "break":
                  return CiToken.Break;
                case "case":
                  return CiToken.Case;
                case "class":
                  return CiToken.Class;
                case "const":
                  return CiToken.Const;
                case "continue":
                  return CiToken.Continue;
                case "default":
                  return CiToken.Default;
                case "delegate":
                  return CiToken.Delegate;
                case "delete":
                  return CiToken.Delete;
                case "do":
                  return CiToken.Do;
                case "else":
                  return CiToken.Else;
                case "enum":
                  return CiToken.Enum;
                case "for":
                  return CiToken.For;
                case "goto":
                  return CiToken.Goto;
                case "if":
                  return CiToken.If;
                case "internal":
                  return CiToken.Internal;
                case "macro":
                  return CiToken.Macro;
                case "native":
                  return CiToken.Native;
                case "new":
                  return CiToken.New;
                case "override":
                  return CiToken.Override;
                case "public":
                  return CiToken.Public;
                case "return":
                  return CiToken.Return;
                case "static":
                  return CiToken.Static;
                case "switch":
                  return CiToken.Switch;
                case "throw":
                  return CiToken.Throw;
                case "virtual":
                  return CiToken.Virtual;
                case "void":
                  return CiToken.Void;
                case "while":
                  return CiToken.While;
                default:
                  this.CurrentString = s;
                  return CiToken.Id;
              }
            }
          default:
            break;
        }
        throw new ParseException(Here(), "Invalid character");
      }
    }

    void NextPreToken() {
      this.CurrentToken = ReadPreToken();
    }

    bool EatPre(CiToken token) {
      if (See(token)) {
        NextPreToken();
        return true;
      }
      return false;
    }

    bool ParsePrePrimary() {
      if (EatPre(CiToken.CondNot)) return !ParsePrePrimary();
      if (EatPre(CiToken.LeftParenthesis)) {
        bool result = ParsePreOr();
        Check(CiToken.RightParenthesis);
        NextPreToken();
        return result;
      }
      if (See(CiToken.Id)) {
        bool result = this.PreSymbols.Contains(this.CurrentString);
        NextPreToken();
        return result;
      }
      throw new ParseException(Here(), "Invalid preprocessor expression");
    }

    bool ParsePreAnd() {
      bool result = ParsePrePrimary();
      while (EatPre(CiToken.CondAnd)) result &= ParsePrePrimary();
      return result;
    }

    bool ParsePreOr() {
      bool result = ParsePreAnd();
      while (EatPre(CiToken.CondOr)) result |= ParsePreAnd();
      return result;
    }

    bool ParsePreExpr() {
      this.LineMode = true;
      NextPreToken();
      bool result = ParsePreOr();
      Check(CiToken.EndOfLine);
      this.LineMode = false;
      return result;
    }

    void ExpectEndOfLine(string directive) {
      this.LineMode = true;
      CiToken token = ReadPreToken();
      if (token != CiToken.EndOfLine && token != CiToken.EndOfFile) throw new ParseException(Here(), "Unexpected characters after " + directive);
      this.LineMode = false;
    }

    enum PreDirectiveClass {
      IfOrElIf,
      Else
    }

    readonly Stack<PreDirectiveClass> PreStack = new Stack<PreDirectiveClass>();

    void PopPreStack(string directive) {
      try {
        PreDirectiveClass pdc = this.PreStack.Pop();
        if (directive != "#endif" && pdc == PreDirectiveClass.Else) throw new ParseException(Here(), directive + " after #else");
      }
      catch (InvalidOperationException) {
        throw new ParseException(Here(), directive + " with no matching #if");
      }
    }

    void SkipUntilPreMet() {
      for (;;) {
        // we are in a conditional that wasn't met yet
        switch (ReadPreToken()) {
          case CiToken.EndOfFile:
            throw new ParseException(Here(), "Expected #endif, got end of file");
          case CiToken.PreIf:
            ParsePreExpr();
            SkipUntilPreEndIf(false);
            break;
          case CiToken.PreElIf:
            if (ParsePreExpr()) {
              this.PreStack.Push(PreDirectiveClass.IfOrElIf);
              return;
            }
            break;
          case CiToken.PreElse:
            ExpectEndOfLine("#else");
            this.PreStack.Push(PreDirectiveClass.Else);
            return;
          case CiToken.PreEndIf:
            ExpectEndOfLine("#endif");
            return;
        }
      }
    }

    void SkipUntilPreEndIf(bool wasElse) {
      for (;;) {
        // we are in a conditional that was met before
        switch (ReadPreToken()) {
          case CiToken.EndOfFile:
            throw new ParseException(Here(), "Expected #endif, got end of file");
          case CiToken.PreIf:
            ParsePreExpr();
            SkipUntilPreEndIf(false);
            break;
          case CiToken.PreElIf:
            if (wasElse) throw new ParseException(Here(), "#elif after #else");
            ParsePreExpr();
            break;
          case CiToken.PreElse:
            if (wasElse) throw new ParseException(Here(), "#else after #else");
            ExpectEndOfLine("#else");
            wasElse = true;
            break;
          case CiToken.PreEndIf:
            ExpectEndOfLine("#endif");
            return;
        }
      }
    }

    protected CiToken ReadToken() {
      for (;;) {
        // we are in no conditionals or in all met
        CiToken token = ReadPreToken();
        switch (token) {
          case CiToken.EndOfFile:
            if (this.PreStack.Count != 0) throw new ParseException(Here(), "Expected #endif, got end of file");
            return CiToken.EndOfFile;
          case CiToken.PreIf:
            if (ParsePreExpr()) {
              this.PreStack.Push(PreDirectiveClass.IfOrElIf);
              break;
            }
            else SkipUntilPreMet();
            break;
          case CiToken.PreElIf:
            PopPreStack("#elif");
            ParsePreExpr();
            SkipUntilPreEndIf(false);
            break;
          case CiToken.PreElse:
            PopPreStack("#else");
            ExpectEndOfLine("#else");
            SkipUntilPreEndIf(true);
            break;
          case CiToken.PreEndIf:
            PopPreStack("#endif");
            ExpectEndOfLine("#endif");
            break;
          default:
            return token;
        }
      }
    }

    public virtual CiToken NextToken() {
      CiToken token = ReadToken();
      this.CurrentToken = token;
      return token;
    }

    public bool See(CiToken token) {
      return this.CurrentToken == token;
    }

    public bool Eat(CiToken token) {
      if (See(token)) {
        NextToken();
        return true;
      }
      return false;
    }

    public void Check(CiToken expected) {
      if (!See(expected)) {
        throw new ParseException(Here(), "Expected {0}, got {1}", expected, this.CurrentToken);
      }
    }

    public void Expect(CiToken expected) {
      Check(expected);
      NextToken();
    }

    public void DebugLexer() {
      while (this.CurrentToken != CiToken.EndOfFile) {
        Console.WriteLine(this.CurrentToken);
        NextToken();
      }
    }
  }
}

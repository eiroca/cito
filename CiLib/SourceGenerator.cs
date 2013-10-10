// SourceGenerator.cs - base class for code generators
//
// Copyright (C) 2011-2013  Piotr Fusik
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

    void Write(CiProgram program);
  }

  public abstract class BaseGenerator : IGenerator {
    protected static string ToCamelCase(string s) {
      return char.ToLowerInvariant(s[0]) + s.Substring(1);
    }

    public string OutputFile { get; set; }

    public string Namespace { get; set; }

    public TextWriterFactory CreateTextWriter { get; set; }

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

    public abstract void Write(CiProgram program);
    #endregion
    #region TextWriterFactory
    protected virtual void CreateFile(string filename) {
      Open(CreateTextWriter(filename));
      WriteBanner();
      Indent = 0;
    }

    protected virtual void CloseFile() {
      WriteFooter();
      Close();
    }

    protected virtual void WriteBanner() {
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

    protected virtual void Open(TextWriter writer) {
      this.Writer = writer;
      this.Indent = 0;
      curLine = new StringBuilder();
      fullCode = new StringBuilder();
    }

    protected virtual void Close() {
      if (curLine.Length > 0) {
        fullCode.Append(curLine);
      }
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
      string newTxt = curLine.ToString().Trim();
      fullCode.Append(GetIndentStr());
      fullCode.Append(newTxt);
      fullCode.Append(NewLineStr);
      curLine = new StringBuilder();
    }

    protected virtual string GetIndentStr() {
      StringBuilder res = new StringBuilder();
      for (int i = 0; i < this.Indent; i++) {
        res.Append(IndentStr);
      }
      return res.ToString();
    }

    protected virtual void WriteEscaped(string text) {
      foreach (char c in text) {
        switch (c) {
          case '&':
            Write("&amp;");
            break;
          case '<':
            Write("&lt;");
            break;
          case '>':
            Write("&gt;");
            break;
          case '\n':
            WriteLine();
            break;
          default:
            Write(c);
            break;
        }
      }
    }

    protected virtual void WriteLowercase(string s) {
      foreach (char c in s) {
        curLine.Append(char.ToLowerInvariant(c));
      }
    }

    protected virtual void WriteCamelCase(string s) {
      curLine.Append(char.ToLowerInvariant(s[0]));
      curLine.Append(s.Substring(1));
    }

    protected virtual void WriteUppercaseWithUnderscores(string s) {
      bool first = true;
      foreach (char c in s) {
        if (char.IsUpper(c) && !first) {
          curLine.Append('_');
          curLine.Append(c);
        }
        else {
          curLine.Append(char.ToUpperInvariant(c));
        }
        first = false;
      }
    }

    protected virtual void WriteLowercaseWithUnderscores(string s) {
      bool first = true;
      foreach (char c in s) {
        if (char.IsUpper(c)) {
          if (!first) {
            curLine.Append('_');
          }
          curLine.Append(char.ToLowerInvariant(c));
        }
        else {
          curLine.Append(c);
        }
        first = false;
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
    #endregion
 }

  public abstract class SourceGenerator : BaseGenerator, ICiStatementVisitor {
    public SourceGenerator(string aNamespace) : base(aNamespace) {
    }

    public SourceGenerator() : base() {
    }
    #region JavaDoc
    void WriteDoc(string text) {
      foreach (char c in text) {
        switch (c) {
          case '&':
            Write("&amp;");
            break;
          case '<':
            Write("&lt;");
            break;
          case '>':
            Write("&gt;");
            break;
          case '\n':
            WriteLine();
            Write(" * ");
            break;
          default:
            Write(c);
            break;
        }
      }
    }

    void Write(CiDocPara para) {
      foreach (CiDocInline inline in para.Children) {
        CiDocText text = inline as CiDocText;
        if (text != null) {
          WriteDoc(text.Text);
          continue;
        }
        CiDocCode code = inline as CiDocCode;
        if (code != null) {
          Write("<code>");
          WriteDoc(code.Text);
          Write("</code>");
          continue;
        }
        throw new ArgumentException(inline.GetType().Name);
      }
    }

    void Write(CiDocBlock block) {
      CiDocList list = block as CiDocList;
      if (list != null) {
        WriteLine();
        WriteLine(" * <ul>");
        foreach (CiDocPara item in list.Items) {
          Write(" * <li>");
          Write(item);
          WriteLine("</li>");
        }
        WriteLine(" * </ul>");
        Write(" * ");
        return;
      }
      Write((CiDocPara)block);
    }

    void WriteDontClose(CiCodeDoc doc) {
      WriteLine("/**");
      Write(" * ");
      Write(doc.Summary);
      if (doc.Details.Length > 0) {
        WriteLine();
        Write(" * ");
        foreach (CiDocBlock block in doc.Details)
          Write(block);
      }
      WriteLine();
    }

    protected virtual void Write(CiCodeDoc doc) {
      if (doc != null) {
        WriteDontClose(doc);
        WriteLine(" */");
      }
    }

    protected void WriteDoc(CiMethod method) {
      if (method.Documentation != null) {
        WriteDontClose(method.Documentation);
        foreach (CiParam param in method.Signature.Params) {
          if (param.Documentation != null) {
            Write(" * @param ");
            Write(param.Name);
            Write(' ');
            Write(param.Documentation.Summary);
            WriteLine();
          }
        }
        WriteLine(" */");
      }
    }
    #endregion JavaDoc
    protected override void WriteBanner() {
      WriteLine("// Generated automatically with \"cito\". Do not edit.");
    }

    protected void WriteInitializer(CiArrayType type) {
      for (; type != null; type = type.ElementType as CiArrayType) {
        Write('[');
        CiArrayStorageType storageType = type as CiArrayStorageType;
        if (storageType != null) {
          if (storageType.LengthExpr != null)
            Write(storageType.LengthExpr);
          else
            Write(storageType.Length);
        }
        Write(']');
      }
    }

    protected void WriteContent(Array array) {
      for (int i = 0; i < array.Length; i++) {
        if (i > 0) {
          if (i % 16 == 0) {
            WriteLine(",");
            Write('\t');
          }
          else
            Write(", ");
        }
        WriteConst(array.GetValue(i));
      }
    }

    protected virtual void WriteConst(object value) {
      if (value is bool)
        Write((bool)value ? "true" : "false");
      else if (value is byte)
        Write((byte)value);
      else if (value is int)
        Write((int)value);
      else if (value is string) {
        Write('"');
        foreach (char c in (string) value) {
          switch (c) {
            case '\t':
              Write("\\t");
              break;
            case '\r':
              Write("\\r");
              break;
            case '\n':
              Write("\\n");
              break;
            case '\\':
              Write("\\\\");
              break;
            case '\"':
              Write("\\\"");
              break;
            default:
              Write(c);
              break;
          }
        }
        Write('"');
      }
      else if (value is CiEnumValue) {
        CiEnumValue ev = (CiEnumValue)value;
        Write(ev.Type.Name);
        Write('.');
        Write(ev.Name);
      }
      else if (value is Array) {
        Write("{ ");
        WriteContent((Array)value);
        Write(" }");
      }
      else if (value == null)
        Write("null");
      else
        throw new ArgumentException(value.ToString());
    }

    protected virtual CiPriority GetPriority(CiExpr expr) {
      if (expr is CiConstExpr
        || expr is CiConstAccess
        || expr is CiVarAccess
        || expr is CiFieldAccess
        || expr is CiPropertyAccess
        || expr is CiArrayAccess
        || expr is CiMethodCall
        || expr is CiBinaryResourceExpr
        || expr is CiNewExpr) // ?
        return CiPriority.Postfix;
      if (expr is CiUnaryExpr
        || expr is CiCondNotExpr
        || expr is CiPostfixExpr) // ?
        return CiPriority.Prefix;
      if (expr is CiCoercion)
        return GetPriority((CiExpr)((CiCoercion)expr).Inner);
      if (expr is CiBinaryExpr) {
        switch (((CiBinaryExpr)expr).Op) {
          case CiToken.Asterisk:
          case CiToken.Slash:
          case CiToken.Mod:
            return CiPriority.Multiplicative;
          case CiToken.Plus:
          case CiToken.Minus:
            return CiPriority.Additive;
          case CiToken.ShiftLeft:
          case CiToken.ShiftRight:
            return CiPriority.Shift;
          case CiToken.Less:
          case CiToken.LessOrEqual:
          case CiToken.Greater:
          case CiToken.GreaterOrEqual:
            return CiPriority.Ordering;
          case CiToken.Equal:
          case CiToken.NotEqual:
            return CiPriority.Equality;
          case CiToken.And:
            return CiPriority.And;
          case CiToken.Xor:
            return CiPriority.Xor;
          case CiToken.Or:
            return CiPriority.Or;
          case CiToken.CondAnd:
            return CiPriority.CondAnd;
          case CiToken.CondOr:
            return CiPriority.CondOr;
          default:
            throw new ArgumentException(((CiBinaryExpr)expr).Op.ToString());
        }
      }
      if (expr is CiCondExpr)
        return CiPriority.CondExpr;
      throw new ArgumentException(expr.GetType().Name);
    }

    protected virtual void WriteChild(CiPriority parentPriority, CiExpr child) {
      if (GetPriority(child) < parentPriority) {
        Write('(');
        Write(child);
        Write(')');
      }
      else
        Write(child);
    }

    protected virtual void WriteChild(CiExpr parent, CiExpr child) {
      WriteChild(GetPriority(parent), child);
    }

    protected void WriteNonAssocChild(CiPriority parentPriority, CiExpr child) {
      if (GetPriority(child) <= parentPriority) {
        Write('(');
        Write(child);
        Write(')');
      }
      else
        Write(child);
    }

    protected void WriteNonAssocChild(CiExpr parent, CiExpr child) {
      WriteNonAssocChild(GetPriority(parent), child);
    }

    protected void WriteSum(CiExpr left, CiExpr right) {
      Write(new CiBinaryExpr { Left = left, Op = CiToken.Plus, Right = right });
    }

    protected virtual void WriteName(CiConst konst) {
      Write(konst.GlobalName ?? konst.Name);
    }

    protected virtual void Write(CiVarAccess expr) {
      Write(expr.Var.Name);
    }

    protected virtual void Write(CiFieldAccess expr) {
      WriteChild(expr, expr.Obj);
      Write('.');
      Write(expr.Field.Name);
    }

    protected abstract void Write(CiPropertyAccess expr);

    protected virtual void Write(CiArrayAccess expr) {
      WriteChild(expr, expr.Array);
      Write('[');
      Write(expr.Index);
      Write(']');
    }

    protected virtual void WriteName(CiMethod method) {
      Write(method.Name);
    }

    protected virtual void WriteDelegateCall(CiExpr expr) {
      Write(expr);
    }

    protected void WriteMulDiv(CiPriority firstPriority, CiMethodCall expr) {
      WriteChild(firstPriority, expr.Obj);
      Write(" * ");
      WriteChild(CiPriority.Multiplicative, expr.Arguments[0]);
      Write(" / ");
      WriteNonAssocChild(CiPriority.Multiplicative, expr.Arguments[1]);
      Write(')');
    }

    protected virtual void WriteArguments(CiMethodCall expr) {
      Write('(');
      bool first = true;
      foreach (CiExpr arg in expr.Arguments) {
        if (first)
          first = false;
        else
          Write(", ");
        Write(arg);
      }
      Write(')');
    }

    protected virtual void Write(CiMethodCall expr) {
      if (expr.Method != null) {
        if (expr.Obj != null)
          Write(expr.Obj);
        else
          Write(expr.Method.Class.Name);
        Write('.');
        WriteName(expr.Method);
      }
      else
        WriteDelegateCall(expr.Obj);
      WriteArguments(expr);
    }

    protected virtual void Write(CiUnaryExpr expr) {
      switch (expr.Op) {
        case CiToken.Increment:
          Write("++");
          break;
        case CiToken.Decrement:
          Write("--");
          break;
        case CiToken.Minus:
          Write('-');
          break;
        case CiToken.Not:
          Write('~');
          break;
        default:
          throw new ArgumentException(expr.Op.ToString());
      }
      WriteChild(expr, expr.Inner);
    }

    protected virtual void Write(CiCondNotExpr expr) {
      Write('!');
      WriteChild(expr, expr.Inner);
    }

    protected virtual void Write(CiPostfixExpr expr) {
      WriteChild(expr, expr.Inner);
      switch (expr.Op) {
        case CiToken.Increment:
          Write("++");
          break;
        case CiToken.Decrement:
          Write("--");
          break;
        default:
          throw new ArgumentException(expr.Op.ToString());
      }
    }

    protected virtual void WriteOp(CiBinaryExpr expr) {
      switch (expr.Op) {
        case CiToken.Plus:
          Write(" + ");
          break;
        case CiToken.Minus:
          Write(" - ");
          return;
        case CiToken.Asterisk:
          Write(" * ");
          break;
        case CiToken.Slash:
          Write(" / ");
          break;
        case CiToken.Mod:
          Write(" % ");
          break;
        case CiToken.ShiftLeft:
          Write(" << ");
          break;
        case CiToken.ShiftRight:
          Write(" >> ");
          break;
        case CiToken.Less:
          Write(" < ");
          break;
        case CiToken.LessOrEqual:
          Write(" <= ");
          break;
        case CiToken.Greater:
          Write(" > ");
          break;
        case CiToken.GreaterOrEqual:
          Write(" >= ");
          break;
        case CiToken.Equal:
          Write(" == ");
          break;
        case CiToken.NotEqual:
          Write(" != ");
          break;
        case CiToken.And:
          Write(" & ");
          break;
        case CiToken.Or:
          Write(" | ");
          break;
        case CiToken.Xor:
          Write(" ^ ");
          break;
        case CiToken.CondAnd:
          Write(" && ");
          break;
        case CiToken.CondOr:
          Write(" || ");
          break;
        default:
          throw new ArgumentException(expr.Op.ToString());
      }
    }

    protected virtual void Write(CiBinaryExpr expr) {
      WriteChild(expr, expr.Left);
      switch (expr.Op) {
        case CiToken.Plus:
        case CiToken.Asterisk:
        case CiToken.Less:
        case CiToken.LessOrEqual:
        case CiToken.Greater:
        case CiToken.GreaterOrEqual:
        case CiToken.Equal:
        case CiToken.NotEqual:
        case CiToken.And:
        case CiToken.Or:
        case CiToken.Xor:
        case CiToken.CondAnd:
        case CiToken.CondOr:
          WriteOp(expr);
          WriteChild(expr, expr.Right);
          break;
        case CiToken.Minus:
        case CiToken.Slash:
        case CiToken.Mod:
        case CiToken.ShiftLeft:
        case CiToken.ShiftRight:
          WriteOp(expr);
          WriteNonAssocChild(expr, expr.Right);
          break;
        default:
          throw new ArgumentException(expr.Op.ToString());
      }
    }

    protected virtual void Write(CiCondExpr expr) {
      WriteNonAssocChild(expr, expr.Cond);
      Write(" ? ");
      WriteChild(expr, expr.OnTrue);
      Write(" : ");
      WriteChild(expr, expr.OnFalse);
    }

    protected virtual void WriteName(CiBinaryResource resource) {
      Write("CiBinaryResource_");
      foreach (char c in resource.Name)
        Write(CiLexer.IsLetter(c) ? c : '_');
    }

    protected virtual void Write(CiBinaryResourceExpr expr) {
      WriteName(expr.Resource);
    }

    protected abstract void WriteNew(CiType type);

    protected void WriteInline(CiMaybeAssign expr) {
      if (expr is CiExpr)
        Write((CiExpr)expr);
      else
        Visit((CiAssign)expr);
    }

    protected virtual void Write(CiCoercion expr) {
      WriteInline(expr.Inner);
    }

    protected virtual void Write(CiExpr expr) {
      if (expr is CiConstExpr)
        WriteConst(((CiConstExpr)expr).Value);
      else if (expr is CiConstAccess)
        WriteName(((CiConstAccess)expr).Const);
      else if (expr is CiVarAccess)
        Write((CiVarAccess)expr);
      else if (expr is CiFieldAccess)
        Write((CiFieldAccess)expr);
      else if (expr is CiPropertyAccess)
        Write((CiPropertyAccess)expr);
      else if (expr is CiArrayAccess)
        Write((CiArrayAccess)expr);
      else if (expr is CiMethodCall)
        Write((CiMethodCall)expr);
      else if (expr is CiUnaryExpr)
        Write((CiUnaryExpr)expr);
      else if (expr is CiCondNotExpr)
        Write((CiCondNotExpr)expr);
      else if (expr is CiPostfixExpr)
        Write((CiPostfixExpr)expr);
      else if (expr is CiBinaryExpr)
        Write((CiBinaryExpr)expr);
      else if (expr is CiCondExpr)
        Write((CiCondExpr)expr);
      else if (expr is CiBinaryResourceExpr)
        Write((CiBinaryResourceExpr)expr);
      else if (expr is CiNewExpr)
        WriteNew(((CiNewExpr)expr).NewType);
      else if (expr is CiCoercion)
        Write((CiCoercion)expr);
      else
        throw new ArgumentException(expr.ToString());
    }

    protected void Write(ICiStatement[] statements, int length) {
      for (int i = 0; i < length; i++)
        Write(statements[i]);
    }

    protected virtual void Write(ICiStatement[] statements) {
      Write(statements, statements.Length);
    }

    public virtual void Visit(CiBlock block) {
      OpenBlock();
      Write(block.Statements);
      CloseBlock();
    }

    protected virtual void WriteChild(ICiStatement stmt) {
      if (stmt is CiBlock) {
        Write(' ');
        Write((CiBlock)stmt);
      }
      else {
        WriteLine();
        OpenBlock(false);
        Write(stmt);
        CloseBlock(false);
      }
    }

    public virtual void Visit(CiExpr expr) {
      Write(expr);
    }

    public abstract void Visit(CiVar stmt);

    public virtual void Visit(CiAssign assign) {
      Write(assign.Target);
      switch (assign.Op) {
        case CiToken.Assign:
          Write(" = ");
          break;
        case CiToken.AddAssign:
          Write(" += ");
          break;
        case CiToken.SubAssign:
          Write(" -= ");
          break;
        case CiToken.MulAssign:
          Write(" *= ");
          break;
        case CiToken.DivAssign:
          Write(" /= ");
          break;
        case CiToken.ModAssign:
          Write(" %= ");
          break;
        case CiToken.ShiftLeftAssign:
          Write(" <<= ");
          break;
        case CiToken.ShiftRightAssign:
          Write(" >>= ");
          break;
        case CiToken.AndAssign:
          Write(" &= ");
          break;
        case CiToken.OrAssign:
          Write(" |= ");
          break;
        case CiToken.XorAssign:
          Write(" ^= ");
          break;
        default:
          throw new ArgumentException(assign.Op.ToString());
      }
      WriteInline(assign.Source);
    }

    public virtual void Visit(CiDelete stmt) {
      // do nothing - assume automatic garbage collector
    }

    public virtual void Visit(CiBreak stmt) {
      WriteLine("break;");
    }

    public virtual void Visit(CiConst stmt) {
    }

    public virtual void Visit(CiContinue stmt) {
      WriteLine("continue;");
    }

    public virtual void Visit(CiDoWhile stmt) {
      Write("do");
      WriteChild(stmt.Body);
      Write("while (");
      Write(stmt.Cond);
      WriteLine(");");
    }

    public virtual void Visit(CiFor stmt) {
      Write("for (");
      if (stmt.Init != null)
        stmt.Init.Accept(this);
      Write(';');
      if (stmt.Cond != null) {
        Write(' ');
        Write(stmt.Cond);
      }
      Write(';');
      if (stmt.Advance != null) {
        Write(' ');
        stmt.Advance.Accept(this);
      }
      Write(')');
      WriteChild(stmt.Body);
    }

    protected virtual void WriteIfOnTrue(CiIf stmt) {
      WriteChild(stmt.OnTrue);
    }

    public virtual void Visit(CiIf stmt) {
      Write("if (");
      Write(stmt.Cond);
      Write(')');
      WriteIfOnTrue(stmt);
      if (stmt.OnFalse != null) {
        Write("else");
        if (stmt.OnFalse is CiIf) {
          Write(' ');
          Write(stmt.OnFalse);
        }
        else
          WriteChild(stmt.OnFalse);
      }
    }

    void ICiStatementVisitor.Visit(CiNativeBlock statement) {
      Write(statement.Content);
    }

    public virtual void Visit(CiReturn stmt) {
      if (stmt.Value == null)
        WriteLine("return;");
      else {
        Write("return ");
        Write(stmt.Value);
        WriteLine(";");
      }
    }

    protected virtual void StartSwitch(CiSwitch stmt) {
    }

    protected virtual void StartCase(ICiStatement stmt) {
    }

    protected virtual void WriteFallthrough(CiExpr expr) {
    }

    protected virtual void EndSwitch(CiSwitch stmt) {
    }

    public virtual void Visit(CiSwitch stmt) {
      Write("switch (");
      Write(stmt.Value);
      WriteLine(") {");
      StartSwitch(stmt);
      foreach (CiCase kase in stmt.Cases) {
        foreach (object value in kase.Values) {
          Write("case ");
          WriteConst(value);
          WriteLine(":");
        }
        OpenBlock(false);
        StartCase(kase.Body[0]);
        Write(kase.Body);
        if (kase.Fallthrough) {
          WriteFallthrough(kase.FallthroughTo);
        }
        CloseBlock(false);
      }
      if (stmt.DefaultBody != null) {
        WriteLine("default:");
        OpenBlock(false);
        StartCase(stmt.DefaultBody[0]);
        Write(stmt.DefaultBody);
        CloseBlock(false);
      }
      EndSwitch(stmt);
      WriteLine("}");
    }

    public abstract void Visit(CiThrow stmt);

    public virtual void Visit(CiWhile stmt) {
      Write("while (");
      Write(stmt.Cond);
      Write(')');
      WriteChild(stmt.Body);
    }

    protected virtual void Write(ICiStatement stmt) {
      stmt.Accept(this);
      if ((stmt is CiMaybeAssign || stmt is CiVar) && (curLine.Length > 0)) {
        WriteLine(";");
      }
    }

    protected virtual void OpenClass(bool isAbstract, CiClass klass, string extendsClause) {
      if (isAbstract)
        Write("abstract ");
      Write("class ");
      Write(klass.Name);
      if (klass.BaseClass != null) {
        Write(extendsClause);
        Write(klass.BaseClass.Name);
      }
      WriteLine();
      OpenBlock();
    }
  }
}

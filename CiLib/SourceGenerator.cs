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

  public  class CiGenerator : DelegateGenerator {
    public CiGenerator(string aNamespace) : this() {
      SetNamespace(aNamespace);
    }

    public CiGenerator() : base() {
      Namespace = "ci";
      BlockCloseStr = "}";
      BlockOpenStr = "{";
      BlockCloseCR = true;
      BlockOpenCR = true;
      TranslateType = TypeTranslator;
      TranslateSymbolName = base.SymbolNameTranslator;
    }

    public override string[] GetReservedWords() {
      String[] result = new String[] {
        "delete",
        "break",
        "continue",
        "do",
        "while",
        "for",
        "if",
        "native",
        "return",
        "switch",
        "case",
        "default",
        "throw"
      };
      return result;
    }

    public TypeInfo TypeTranslator(CiType type) {
      TypeInfo info = new TypeInfo();
      info.Type = type;
      info.IsNative = true;
      info.Level = 0;
      CiType elem = type;
      if (type.ArrayLevel > 0) {
        info.IsNative = false;
        info.Level = type.ArrayLevel;
        elem = type.BaseType;
      }
      if (elem is CiStringType) {
        info.Name = "string";
        info.Definition = "string";
        info.ItemDefault = "''";
        info.ItemType = "string";
        info.Null = "null";
      }
      else if (elem == CiBoolType.Value) {
        info.Name = "bool";
        info.Definition = "bool";
        info.ItemDefault = "false";
        info.ItemType = "bool";
        info.Null = "false";
      }
      else if (elem == CiByteType.Value) {
        info.Name = "byte";
        info.Definition = "byte";
        info.ItemDefault = "0";
        info.ItemType = "byte";
        info.Null = "0";
      }
      else if (elem == CiIntType.Value) {
        info.Name = "int";
        info.Definition = "int";
        info.ItemDefault = "0";
        info.ItemType = "int";
        info.Null = "0";
      }
      else if (elem is CiEnum) {
        var ev = ((CiEnum)elem).Values[0];
        info.IsNative = false;
        info.Name = elem.Name;
        info.Definition = elem.Name;
        info.ItemDefault = ev.Type.Name + "." + ev.Name;
        info.ItemType = elem.Name;
        info.Null = info.ItemDefault;
      }
      else {
        info.Name = elem.Name;
        info.Definition = elem.Name;
        info.ItemDefault = "null";
        info.Null = "null";
        info.ItemType = elem.Name;
      }
      return info;
    }

    protected override void WriteBanner() {
      WriteLine("// Generated automatically with \"cito\". Do not edit.");
    }

    public override void InitOperators() {
      BinaryOperators.Add(CiToken.Plus, CiPriority.Additive, ConvertOperatorAssociative, " + ");
      BinaryOperators.Add(CiToken.Minus, CiPriority.Additive, ConvertOperatorNotAssociative, " - ");
      BinaryOperators.Add(CiToken.Asterisk, CiPriority.Multiplicative, ConvertOperatorAssociative, " * ");
      BinaryOperators.Add(CiToken.Slash, CiPriority.Multiplicative, ConvertOperatorNotAssociative, " / ");
      BinaryOperators.Add(CiToken.Mod, CiPriority.Multiplicative, ConvertOperatorNotAssociative, " % ");
      BinaryOperators.Add(CiToken.ShiftLeft, CiPriority.Shift, ConvertOperatorNotAssociative, " << ");
      BinaryOperators.Add(CiToken.ShiftRight, CiPriority.Shift, ConvertOperatorNotAssociative, " >> ");
      //
      BinaryOperators.Add(CiToken.Equal, CiPriority.Equality, ConvertOperatorAssociative, " == ");
      BinaryOperators.Add(CiToken.NotEqual, CiPriority.Equality, ConvertOperatorAssociative, " != ");
      BinaryOperators.Add(CiToken.Less, CiPriority.Ordering, ConvertOperatorAssociative, " < ");
      BinaryOperators.Add(CiToken.LessOrEqual, CiPriority.Ordering, ConvertOperatorAssociative, " <= ");
      BinaryOperators.Add(CiToken.Greater, CiPriority.Ordering, ConvertOperatorNotAssociative, " > ");
      BinaryOperators.Add(CiToken.GreaterOrEqual, CiPriority.Ordering, ConvertOperatorAssociative, " >= ");
      BinaryOperators.Add(CiToken.CondAnd, CiPriority.CondAnd, ConvertOperatorAssociative, " && ");
      BinaryOperators.Add(CiToken.CondOr, CiPriority.CondOr, ConvertOperatorAssociative, " || ");
      //
      BinaryOperators.Add(CiToken.And, CiPriority.Multiplicative, ConvertOperatorNotAssociative, " & ");
      BinaryOperators.Add(CiToken.Or, CiPriority.Multiplicative, ConvertOperatorNotAssociative, " | ");
      BinaryOperators.Add(CiToken.Xor, CiPriority.Multiplicative, ConvertOperatorNotAssociative, " ^ ");
      //
      UnaryOperators.Add(CiToken.Increment, CiPriority.Prefix, ConvertOperatorUnary, "++", "");
      UnaryOperators.Add(CiToken.Decrement, CiPriority.Prefix, ConvertOperatorUnary, "--", "");
      UnaryOperators.Add(CiToken.Minus, CiPriority.Prefix, ConvertOperatorUnary, "-", "");
      UnaryOperators.Add(CiToken.Not, CiPriority.Prefix, ConvertOperatorUnary, "~", "");
    }

    public override void EmitProgram(CiProgram prog) {
      CreateFile(this.OutputFile);
      foreach (CiSymbol symbol in prog.Globals) {
        Translate(symbol);
      }
      CloseFile();
    }

    #region Converter Expression
    public virtual void Expression_CiConstExpr(CiExpr expression) {
      CiConstExpr expr = (CiConstExpr)expression;
      Write(DecodeValue(GetExprType(expr), expr.Value));
    }

    public virtual void Expression_CiConstAccess(CiExpr expression) {
      CiConstAccess expr = (CiConstAccess)expression;
      Write(DecodeSymbol(expr.Const));
    }

    public virtual void Expression_CiVarAccess(CiExpr expression) {
      CiVarAccess expr = (CiVarAccess)expression;
      Write(DecodeSymbol(expr.Var));
    }

    public virtual void Expression_CiFieldAccess(CiExpr expression) {
      CiFieldAccess expr = (CiFieldAccess)expression;
      WriteChild(expr, expr.Obj);
      Write('.');
      Write(DecodeSymbol(expr.Field));
    }

    public virtual void Expression_CiPropertyAccess(CiExpr expression) {
      CiPropertyAccess expr = (CiPropertyAccess)expression;
      if (!Translate(expr)) {
        throw new ArgumentException(expr.Property.Name);
      }
    }

    public virtual void Expression_CiArrayAccess(CiExpr expression) {
      CiArrayAccess expr = (CiArrayAccess)expression;
      WriteChild(expr, expr.Array);
      Write('[');
      Translate(expr.Index);
      Write(']');
    }

    public virtual void Expression_CiMethodCall(CiExpr expression) {
      CiMethodCall expr = (CiMethodCall)expression;
      if (!Translate(expr)) {
        if (expr.Method != null) {
          if (expr.Obj != null) {
            Translate(expr.Obj);
          }
          else {
            Write(DecodeSymbol(expr.Method.Class));
          }
          Write('.');
          Write(DecodeSymbol(expr.Method));
        }
        else {
          Translate(expr.Obj);
        }
        WriteArguments(expr);
      }
    }

    public virtual void Expression_CiBinaryResourceExpr(CiExpr expression) {
      CiBinaryResourceExpr expr = (CiBinaryResourceExpr)expression;
      Write(DecodeSymbol(expr.Resource));
    }

    public virtual void Expression_CiNewExpr(CiExpr expression) {
      CiNewExpr expr = (CiNewExpr)expression;
      WriteNew(expr.NewType);
    }

    public virtual void Expression_CiUnaryExpr(CiExpr expression) {
      CiUnaryExpr expr = (CiUnaryExpr)expression;
      UnaryOperatorInfo tokenInfo = UnaryOperators.GetUnaryOperator(expr.Op);
      tokenInfo.WriteDelegate(expr, tokenInfo);
    }

    public virtual void Expression_CiCondNotExpr(CiExpr expression) {
      CiCondNotExpr expr = (CiCondNotExpr)expression;
      Write('!');
      WriteChild(expr, expr.Inner);
    }

    public void Expression_CiPostfixExpr(CiExpr expression) {
      CiPostfixExpr expr = (CiPostfixExpr)expression;
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

    public virtual void Expression_CiCondExpr(CiExpr expression) {
      CiCondExpr expr = (CiCondExpr)expression;
      WriteChild(expr, expr.Cond, true);
      Write(" ? ");
      WriteChild(expr, expr.OnTrue);
      Write(" : ");
      WriteChild(expr, expr.OnFalse);
    }

    public virtual void Expression_CiBinaryExpr(CiExpr expression) {
      CiBinaryExpr expr = (CiBinaryExpr)expression;
      BinaryOperatorInfo tokenInfo = BinaryOperators.GetBinaryOperator(expr.Op);
      tokenInfo.WriteDelegate(expr, tokenInfo);
    }

    public virtual void Expression_CiCoercion(CiExpr expression) {
      CiCoercion expr = (CiCoercion)expression;
      WriteInline(expr.Inner);
    }
    #endregion

    #region Converter Symbols
    public virtual void Symbol_CiEnum(CiSymbol symbol) {
      CiEnum enu = (CiEnum)symbol;
      WriteLine();
      Write(enu.Documentation);
      WriteFormat("{1} enum {0} ", DecodeSymbol(enu), DecodeVisibility(enu.Visibility));
      OpenBlock();
      bool first = true;
      foreach (CiEnumValue value in enu.Values) {
        if (first) {
          first = false;
        }
        else {
          WriteLine(",");
        }
        Write(value.Documentation);
        Write(value.Name);
      }
      WriteLine();
      CloseBlock();
    }

    public virtual void Symbol_CiConst(CiSymbol symbol) {
      CiConst konst = (CiConst)symbol;
      Write(konst.Documentation);
      WriteLine("public const {0} {1} = {2};", DecodeType(konst.Type), DecodeSymbol(konst), DecodeValue(konst.Type, konst.Value));
    }

    public virtual void Symbol_CiField(CiSymbol symbol) {
      CiField field = (CiField)symbol;
      Write(field.Documentation);
      WriteFormat("{0} {1} {2}", DecodeVisibility(field.Visibility), DecodeType(field.Type), DecodeSymbol(field));
      WriteInit(field.Type);
      WriteLine(";");
    }

    public virtual void Symbol_CiMacro(CiSymbol symbol) {
      CiMacro macro = (CiMacro)symbol;
      Write("//Expanded " + macro.Name);
    }

    public virtual void Symbol_CiMethod(CiSymbol symbol) {
      CiMethod method = (CiMethod)symbol;
      WriteLine();
      Write(method.Documentation);
      foreach (CiParam param in method.Signature.Params) {
        if (param.Documentation != null) {
          WriteFormat("/// <param name=\"{0}\">", param.Name);
          Write(param.Documentation.Summary);
          WriteLine("</param>");
        }
      }
      string callType = "";
      switch (method.CallType) {
        case CiCallType.Static:
          callType = "static ";
          break;
        case CiCallType.Normal:
          break;
        case CiCallType.Abstract:
          callType = "abstract ";
          break;
        case CiCallType.Virtual:
          callType = "virtual ";
          break;
        case CiCallType.Override:
          callType = "override ";
          break;
      }
      WriteFormat("{0} {1}", DecodeVisibility(method.Visibility), callType);
      WriteSignature(method.Signature);
      if (method.CallType == CiCallType.Abstract) {
        WriteLine(";");
      }
      else {
        Write(" ");
        WriteCode(method.Body);
      }
    }

    public virtual void Symbol_CiClass(CiSymbol symbol) {
      CiClass klass = (CiClass)symbol;
      WriteLine();
      Write(klass.Documentation);
      WriteFormat("{0} ", DecodeVisibility(klass.Visibility));
      OpenClass(klass.IsAbstract, klass, " : ");
      if (klass.Constructor != null) {
        WriteFormat("public {0}() ", DecodeSymbol(klass));
        WriteCode(klass.Constructor.Body);
      }
      foreach (CiSymbol member in klass.Members) {
        Translate(member);
      }
      foreach (CiConst konst in klass.ConstArrays) {
        WriteLine("static readonly {0} {1} = {2};", DecodeType(konst.Type), konst.GlobalName, DecodeValue(konst.Type, konst.Value));
      }
      foreach (CiBinaryResource resource in klass.BinaryResources) {
        WriteLine("static readonly byte[] {0} = {1};", DecodeSymbol(resource), DecodeValue(null, resource.Content));
      }
      CloseBlock();
    }

    public virtual void Symbol_CiDelegate(CiSymbol symbol) {
      CiDelegate del = (CiDelegate)symbol;
      Write(del.Documentation);
      WriteFormat("{0} delegate ", DecodeVisibility(del.Visibility));
      WriteSignature(del);
      WriteLine(";");
    }
    #endregion

    #region Converter Statements
    public virtual void Statement_CiBlock(ICiStatement statement) {
      CiBlock block = (CiBlock)statement;
      OpenBlock();
      WriteCode(block.Statements);
      CloseBlock();
    }

    public virtual void Statement_CiVar(ICiStatement statement) {
      CiVar stmt = (CiVar)statement;
      WriteFormat("{0} {1}", DecodeType(stmt.Type), DecodeSymbol(stmt));
      if (!WriteInit(stmt.Type) && stmt.InitialValue != null) {
        Write(" = ");
        Translate(stmt.InitialValue);
      }
    }

    public virtual void Statement_CiExpr(ICiStatement statement) {
      Translate((CiExpr)statement);
    }

    public virtual void Statement_CiAssign(ICiStatement statement) {
      CiAssign assign = (CiAssign)statement;
      Translate(assign.Target);
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

    public virtual void Statement_CiDelete(ICiStatement statement) {
      CiDelete stmt = (CiDelete)statement;
      Write("delete ");
      Translate(stmt.Expr);
      WriteLine(";");
    }

    public virtual void Statement_CiBreak(ICiStatement statement) {
      WriteLine("break;");
    }

    public virtual void Statement_CiContinue(ICiStatement statement) {
      WriteLine("continue;");
    }

    public virtual void Statement_CiDoWhile(ICiStatement statement) {
      CiDoWhile stmt = (CiDoWhile)statement;
      Write("do");
      WriteChild(stmt.Body);
      Write("while (");
      Translate(stmt.Cond);
      WriteLine(");");
    }

    public virtual void Statement_CiFor(ICiStatement statement) {
      CiFor stmt = (CiFor)statement;
      Write("for (");
      if (stmt.Init != null) {
        Translate(stmt.Init);
      }
      Write(';');
      if (stmt.Cond != null) {
        Write(' ');
        Translate(stmt.Cond);
      }
      Write(';');
      if (stmt.Advance != null) {
        Write(' ');
        Translate(stmt.Advance);
      }
      Write(')');
      WriteChild(stmt.Body);
    }

    public virtual void Statement_CiIf(ICiStatement statement) {
      CiIf stmt = (CiIf)statement;
      Write("if (");
      Translate(stmt.Cond);
      Write(')');
      WriteChild(stmt.OnTrue);
      if (stmt.OnFalse != null) {
        Write("else");
        if (stmt.OnFalse is CiIf) {
          Write(' ');
          WriteCode(stmt.OnFalse);
        }
        else {
          WriteChild(stmt.OnFalse);
        }
      }
    }

    public virtual void Statement_CiNativeBlock(ICiStatement statement) {
      CiNativeBlock block = (CiNativeBlock)statement;
      Write(block.Content);
    }

    public virtual void Statement_CiReturn(ICiStatement statement) {
      CiReturn stmt = (CiReturn)statement;
      if (stmt.Value == null) {
        WriteLine("return;");
      }
      else {
        Write("return ");
        Translate(stmt.Value);
        WriteLine(";");
      }
    }

    public virtual void Statement_CiSwitch(ICiStatement statement) {
      CiSwitch swich = (CiSwitch)statement;
      Write("switch (");
      Translate(swich.Value);
      WriteLine(") {");
      StartSwitch(swich);
      foreach (CiCase kase in swich.Cases) {
        foreach (object value in kase.Values) {
          WriteFormat("case {0}:", DecodeValue(null, value));
        }
        OpenBlock(false);
        StartCase(kase.Body[0]);
        WriteCode(kase.Body);
        if (kase.Fallthrough) {
          WriteFallthrough(kase.FallthroughTo);
        }
        CloseBlock(false);
      }
      if (swich.DefaultBody != null) {
        WriteLine("default:");
        OpenBlock(false);
        StartCase(swich.DefaultBody[0]);
        WriteCode(swich.DefaultBody);
        CloseBlock(false);
      }
      EndSwitch(swich);
      WriteLine("}");
    }

    public virtual void Statement_CiThrow(ICiStatement statement) {
      CiThrow stmt = (CiThrow)statement;
      Write("throw ");
      Translate(stmt.Message);
      WriteLine(";");
    }

    public virtual void Statement_CiWhile(ICiStatement statement) {
      CiWhile stmt = (CiWhile)statement;
      Write("while (");
      Translate(stmt.Cond);
      Write(')');
      WriteChild(stmt.Body);
    }
    #endregion

    #region CiTo Library handlers
    public virtual void Library_SByte(CiPropertyAccess expr) {
      WriteChild(expr, expr.Obj);
      Write(".Sbyte");
    }

    public virtual void Library_LowByte(CiPropertyAccess expr) {
      WriteChild(expr, expr.Obj);
      Write(".LowByte");
    }

    public virtual void Library_Length(CiPropertyAccess expr) {
      WriteChild(expr, expr.Obj);
      Write(".Length");
    }

    public virtual void Library_MulDiv(CiMethodCall expr) {
      WriteChild(CiPriority.Prefix, expr.Obj);
      Write(".MulDiv(");
      WriteChild(CiPriority.Multiplicative, expr.Arguments[0]);
      Write(", ");
      WriteChild(CiPriority.Multiplicative, expr.Arguments[1], true);  
      Write(")");
    }

    public virtual void Library_CharAt(CiMethodCall expr) {
      Translate(expr.Obj);
      Write("[");
      Translate(expr.Arguments[0]);
      Write("]");
    }

    public virtual void Library_Substring(CiMethodCall expr) {
      Translate(expr.Obj);
      Write(".Substring(");
      Translate(expr.Arguments[0]);
      Write(", ");
      Translate(expr.Arguments[1]);
      Write(')');
    }

    public virtual void Library_CopyTo(CiMethodCall expr) {
      Translate(expr.Obj);
      Write(".CopyTo(");
      Translate(expr.Arguments[0]);
      Write(", ");
      Translate(expr.Arguments[1]);
      Write(", ");
      Translate(expr.Arguments[2]);
      Write(", ");
      Translate(expr.Arguments[3]);
      Write(')');
    }

    public virtual void Library_ToString(CiMethodCall expr) {
      Translate(expr.Obj);
      Write(".ToString(");
      Translate(expr.Arguments[0]);
      Write(", ");
      Translate(expr.Arguments[1]);
      Write(")");
    }

    public virtual void Library_Clear(CiMethodCall expr) {
      Translate(expr.Obj);
      Write(".Clear()");
    }
    #endregion

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
            Write("/// ");
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
          Write("`");
          WriteDoc(code.Text);
          Write("`");
          continue;
        }
        throw new ArgumentException(inline.GetType().Name);
      }
    }

    void Write(CiDocBlock block) {
      CiDocList list = block as CiDocList;
      if (list != null) {
        WriteLine();
        foreach (CiDocPara item in list.Items) {
          Write("/// * ");
          Write(item);
          WriteLine("");
        }
        Write("///");
        return;
      }
      Write("/// ");
      Write((CiDocPara)block);
    }

    void WriteDontClose(CiCodeDoc doc) {
      Write("/// ");
      Write(doc.Summary);
      if (doc.Details.Length > 0) {
        WriteLine();
        foreach (CiDocBlock block in doc.Details) {
          Write(block);
        }
      }
      WriteLine();
    }

    protected virtual void Write(CiCodeDoc doc) {
      if (doc != null) {
        WriteDontClose(doc);
      }
    }

    protected void WriteDoc(CiMethod method) {
      if (method.Documentation != null) {
        WriteDontClose(method.Documentation);
        foreach (CiParam param in method.Signature.Params) {
          if (param.Documentation != null) {
            Write("/// @param ");
            Write(param.Name);
            Write(' ');
            Write(param.Documentation.Summary);
            WriteLine();
          }
        }
        WriteLine("");
      }
    }
    #endregion JavaDoc

    public virtual string DecodeVisibility(CiVisibility visibility) {
      string res;
      switch (visibility) {
        case CiVisibility.Dead:
          res = "private";
          break;
        case CiVisibility.Private:
          res = "private";
          break;
        case CiVisibility.Internal:
          res = "private";
          break;
        case CiVisibility.Public:
          res = "public";
          break;
        default:
          res = "???";
          break;
      }
      return res;
    }

    protected virtual void StartSwitch(CiSwitch stmt) {
    }

    protected virtual void StartCase(ICiStatement stmt) {
    }

    protected virtual void WriteFallthrough(CiExpr expr) {
    }

    protected virtual void EndSwitch(CiSwitch stmt) {
    }

    public virtual void WriteSignature(CiDelegate del) {
      WriteFormat("{0} {1}", DecodeType(del.ReturnType), DecodeSymbol(del));
      Write('(');
      bool first = true;
      foreach (CiParam param in del.Params) {
        if (first) {
          first = false;
        }
        else {
          Write(", ");
        }
        WriteFormat("{0} {1}", DecodeType(param.Type), DecodeSymbol(param));
      }
      Write(')');
    }

    public virtual bool WriteInit(CiType type) {
      if (type is CiClassStorageType || type is CiArrayStorageType) {
        Write(" = ");
        WriteNew(type);
        return true;
      }
      return false;
    }

    public virtual void WriteNew(CiType type) {
      CiClassStorageType classType = type as CiClassStorageType;
      if (classType != null) {
        Write("new ");
        Write(classType.Class.Name);
        Write("()");
      }
      else {
        CiArrayStorageType arrayType = (CiArrayStorageType)type;
        Write("new ");
        WriteBaseType(arrayType.BaseType);
        WriteInitializer(arrayType);
      }

    }

    void WriteBaseType(CiType type) {
      if (type is CiStringType) {
        Write("string");
      }
      else {
        Write(type.Name);
      }
    }

    protected void WriteInitializer(CiArrayType type) {
      for (; type != null; type = type.ElementType as CiArrayType) {
        Write('[');
        CiArrayStorageType storageType = type as CiArrayStorageType;
        if (storageType != null) {
          if (storageType.LengthExpr != null) {
            Translate(storageType.LengthExpr);
          }
          else {
            Write(storageType.Length);
          }
        }
        Write(']');
      }
    }

    public override string DecodeValue(CiType type, object value) {
      StringBuilder res = new StringBuilder();
      if (value is bool) {
        res.Append((bool)value ? "true" : "false");
      }
      else if (value is byte) {
        res.Append((byte)value);
      }
      else if (value is int) {
        res.Append((int)value);
      }
      else if (value is string) {
        res.Append('"');
        foreach (char c in (string) value) {
          switch (c) {
            case '\t':
              res.Append("\\t");
              break;
            case '\r':
              res.Append("\\r");
              break;
            case '\n':
              res.Append("\\n");
              break;
            case '\\':
              res.Append("\\\\");
              break;
            case '\"':
              res.Append("\\\"");
              break;
            default:
              res.Append(c);
              break;
          }
        }
        res.Append('"');
      }
      else if (value is CiEnumValue) {
        CiEnumValue ev = (CiEnumValue)value;
        res.Append(ev.Type.Name);
        res.Append('.');
        res.Append(ev.Name);
      }
      else if (value is Array) {
        res.Append("{ ");
        res.Append(DecodeArray(type, (Array)value));
        res.Append(" }");
      }
      else if (value == null) {
        res.Append("null");
      }
      else {
        throw new ArgumentException(value.ToString());
      }
      return res.ToString();
    }

    public override CiPriority GetPriority(CiExpr expr) {
      if (expr is CiPropertyAccess) {
        CiProperty prop = ((CiPropertyAccess)expr).Property;
        if (prop == CiLibrary.SByteProperty || prop == CiLibrary.LowByteProperty) {
          return CiPriority.Prefix;
        }
      }
      else if (expr is CiCoercion) {
        CiCoercion c = (CiCoercion)expr;
        if (c.ResultType == CiByteType.Value && c.Inner.Type == CiIntType.Value) {
          return CiPriority.Prefix;
        }
      }
      return base.GetPriority(expr);
    }

    protected virtual void WriteArguments(CiMethodCall expr) {
      Write('(');
      bool first = true;
      foreach (CiExpr arg in expr.Arguments) {
        if (first) {
          first = false;
        }
        else {
          Write(", ");
        }
        Translate(arg);
      }
      Write(')');
    }

    protected void WriteInline(CiMaybeAssign expr) {
      if (expr is CiExpr) {
        Translate((CiExpr)expr);
      }
      else {
        Translate((CiAssign)expr);
      }
    }

    protected void WriteCode(ICiStatement[] statements, int length) {
      for (int i = 0; i < length; i++) {
        WriteCode(statements[i]);
      }
    }

    protected void WriteCode(ICiStatement[] statements) {
      WriteCode(statements, statements.Length);
    }

    protected virtual void WriteChild(ICiStatement stmt) {
      if (stmt is CiBlock) {
        Write(' ');
        WriteCode(stmt);
      }
      else {
        Write(' ');
        OpenBlock(true);
        WriteCode(stmt);
        CloseBlock(true);
      }
    }

    protected virtual void WriteCode(ICiStatement stmt) {
      if (stmt == null) {
        return;
      }
      Translate(stmt);
      if ((stmt is CiMaybeAssign || stmt is CiVar) && (curLine.Length > 0)) {
        WriteLine(";");
      }
    }

    protected virtual void OpenClass(bool isAbstract, CiClass klass, string extendsClause) {
      if (isAbstract) {
        Write("abstract ");
      }
      Write("class ");
      Write(klass.Name);
      if (klass.BaseClass != null) {
        Write(extendsClause);
        Write(klass.BaseClass.Name);
      }
      Write(" ");
      OpenBlock();
    }
  }
}

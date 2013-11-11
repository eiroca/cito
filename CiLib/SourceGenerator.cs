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

  public  class CiGenerator : DelegateGenerator {
    protected  bool OrderClass = false;

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
      OrderClass = false;
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
        info.Null = "null";
        info.ItemType = info.Name;
        info.ItemDefault = "''";
      }
      else if (elem == CiBoolType.Value) {
        info.Name = "bool";
        info.Null = "false";
        info.ItemType = info.Name;
        info.ItemDefault = info.Null;
      }
      else if (elem == CiIntType.Value) {
        info.Name = "int";
        info.Null = "0";
        info.ItemType = info.Name;
        info.ItemDefault = info.Null;
      }
      else if (elem == CiByteType.Value) {
        info.Name = "byte";
        info.Null = "0";
        info.ItemType = info.Name;
        info.ItemDefault = info.Null;
      }
      else if (elem is CiEnum) {
        var ev = ((CiEnum)elem).Values[0];
        info.IsNative = false;
        info.Name = elem.Name;
        info.Null = info.ItemDefault;
        info.ItemType = info.Name;
        info.ItemDefault = ev.Type.Name + "." + ev.Name;
      }
      else {
        info.Name = elem.Name;
        info.Null = "null";
        info.ItemType = elem.Name;
        info.ItemDefault = info.Null;
      }
      if (type is CiClassStorageType) {
        info.Definition = info.Name;
        info.Name = info.Name + "()";
      } 
      if (type is CiArrayStorageType) {
        info.Definition = info.Name;
        if (info.Level > 0) {
          for (int i=0; i<info.Level; i++) {
            info.Name = info.Name + "[{" + i + "}]";
          }
        }
      }
      else {
        if (info.Level > 0) {
          info.Definition = info.Name;
          for (int i=0; i<info.Level; i++) {
            info.Name = info.Name + "[]";
          }
        }
      }
      return info;
    }

    protected override void WriteBanner() {
      WriteLine("// Generated automatically with \"cito\". Do not edit.");
    }

    public override void InitOperators() {
      BinaryOperators.Declare(CiToken.Plus, CiPriority.Additive, ConvertOperatorAssociative, " + ");
      BinaryOperators.Declare(CiToken.Minus, CiPriority.Additive, ConvertOperatorNotAssociative, " - ");
      BinaryOperators.Declare(CiToken.Asterisk, CiPriority.Multiplicative, ConvertOperatorAssociative, " * ");
      BinaryOperators.Declare(CiToken.Slash, CiPriority.Multiplicative, ConvertOperatorNotAssociative, " / ");
      BinaryOperators.Declare(CiToken.Mod, CiPriority.Multiplicative, ConvertOperatorNotAssociative, " % ");
      BinaryOperators.Declare(CiToken.ShiftLeft, CiPriority.Shift, ConvertOperatorNotAssociative, " << ");
      BinaryOperators.Declare(CiToken.ShiftRight, CiPriority.Shift, ConvertOperatorNotAssociative, " >> ");
      //
      BinaryOperators.Declare(CiToken.Equal, CiPriority.Equality, ConvertOperatorAssociative, " == ");
      BinaryOperators.Declare(CiToken.NotEqual, CiPriority.Equality, ConvertOperatorAssociative, " != ");
      BinaryOperators.Declare(CiToken.Less, CiPriority.Ordering, ConvertOperatorAssociative, " < ");
      BinaryOperators.Declare(CiToken.LessOrEqual, CiPriority.Ordering, ConvertOperatorAssociative, " <= ");
      BinaryOperators.Declare(CiToken.Greater, CiPriority.Ordering, ConvertOperatorNotAssociative, " > ");
      BinaryOperators.Declare(CiToken.GreaterOrEqual, CiPriority.Ordering, ConvertOperatorAssociative, " >= ");
      BinaryOperators.Declare(CiToken.CondAnd, CiPriority.CondAnd, ConvertOperatorAssociative, " && ");
      BinaryOperators.Declare(CiToken.CondOr, CiPriority.CondOr, ConvertOperatorAssociative, " || ");
      //
      BinaryOperators.Declare(CiToken.And, CiPriority.Multiplicative, ConvertOperatorNotAssociative, " & ");
      BinaryOperators.Declare(CiToken.Or, CiPriority.Multiplicative, ConvertOperatorNotAssociative, " | ");
      BinaryOperators.Declare(CiToken.Xor, CiPriority.Multiplicative, ConvertOperatorNotAssociative, " ^ ");
      //
      UnaryOperators.Declare(CiToken.Increment, CiPriority.Prefix, ConvertOperatorUnary, "++", "");
      UnaryOperators.Declare(CiToken.Decrement, CiPriority.Prefix, ConvertOperatorUnary, "--", "");
      UnaryOperators.Declare(CiToken.Minus, CiPriority.Prefix, ConvertOperatorUnary, "-", "");
      UnaryOperators.Declare(CiToken.Not, CiPriority.Prefix, ConvertOperatorUnary, "~", "");
    }

    public override void EmitProgram(CiProgram prog) {
      CreateFile(this.OutputFile);
      if (OrderClass) {
        foreach (CiSymbol symbol in prog.Globals) {
          if (!(symbol is CiClass)) {
            Translate(symbol);
          }
        }
        foreach (CiClass klass in GetOrderedClassList()) {
          Translate(klass);
        }
      }
      else {
        foreach (CiSymbol symbol in prog.Globals) {
          Translate(symbol);
        }
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
      WriteLine("{1} {2};", DecodeVisibility(field.Visibility), DecodeType(field.Type), DecodeSymbol(field));
    }

    public virtual void Symbol_CiMacro(CiSymbol symbol) {
      CiMacro macro = (CiMacro)symbol;
      WriteLine("// Macro: " + macro.Name);
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
      bool hasFields = false;
      foreach (CiSymbol member in klass.Members) {
        if (!(member is CiMethod)) {
          Translate(member);
          hasFields = true;
        }
      }
      if (hasFields) {
        WriteLine();
      }
      if (klass.Constructor != null) {
        WriteFormat("public {0}() ", DecodeSymbol(klass));
        WriteCode(klass.Constructor.Body);
      }
      foreach (CiSymbol member in klass.Members) {
        if (member is CiMethod) {
          Translate(member);
        }
      }
      foreach (CiConst konst in klass.ConstArrays) {
        WriteLine("static readonly {0} {1} = {2};", DecodeType(konst.Type), DecodeSymbol(konst), DecodeValue(konst.Type, konst.Value));
      }
      foreach (CiBinaryResource resource in klass.BinaryResources) {
        WriteLine("static readonly byte[] {0} = {1};", DecodeSymbol(resource), DecodeValue(resource.Type, resource.Content));
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
          WriteLine("case {0}:", DecodeValue(null, value));
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
    protected string CommentContinueStr = "/// ";
    protected string CommentBeginStr = "";
    protected string CommentEndStr = "";
    protected string CommentCodeBegin = "`";
    protected string CommentCodeEnd = "`";

    protected virtual void WriteDoc(string text) {
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
            Write(CommentContinueStr);
            break;
          default:
            Write(c);
            break;
        }
      }
    }

    protected virtual void Write(CiDocPara para) {
      foreach (CiDocInline inline in para.Children) {
        CiDocText text = inline as CiDocText;
        if (text != null) {
          WriteDoc(text.Text);
          continue;
        }
        CiDocCode code = inline as CiDocCode;
        if (code != null) {
          Write(CommentCodeBegin);
          WriteDoc(code.Text);
          Write(CommentCodeEnd);
          continue;
        }
        throw new ArgumentException(inline.GetType().Name);
      }
    }

    protected virtual void Write(CiDocBlock block) {
      CiDocList list = block as CiDocList;
      if (list != null) {
        WriteLine();
        foreach (CiDocPara item in list.Items) {
          Write(CommentContinueStr + "* ");
          Write(item);
          WriteLine("");
        }
        Write(CommentContinueStr);
        return;
      }
      Write(CommentContinueStr);
      Write((CiDocPara)block);
    }

    protected virtual void WriteDontClose(CiCodeDoc doc) {
      WriteLine(CommentBeginStr);
      Write(CommentContinueStr);
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
        WriteLine(CommentEndStr);
      }
    }

    protected virtual void WriteDoc(CiMethod method) {
      if (method.Documentation != null) {
        WriteDontClose(method.Documentation);
        foreach (CiParam param in method.Signature.Params) {
          if (param.Documentation != null) {
            Write(CommentContinueStr + "@param ");
            Write(param.Name);
            Write(' ');
            Write(param.Documentation.Summary);
            WriteLine();
          }
        }
        WriteLine(CommentEndStr);
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
      if (type is CiClassStorageType) {
      }
      else if (type is CiArrayStorageType) {
        // Write(" = "); WriteNew(type); return true;
      }
      return false;
    }

    public virtual void WriteNew(CiType type) {
      CiClassStorageType classType = type as CiClassStorageType;
      if (classType != null) {
        WriteFormat("new {0}()", DecodeSymbol(classType.Class));
      }
      else {
        CiArrayStorageType arrayType = (CiArrayStorageType)type;
        TypeInfo info = GetTypeInfo(type);
        WriteFormat("new {0}", info.ItemType);
        WriteInitializer(arrayType);
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

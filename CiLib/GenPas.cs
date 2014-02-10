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
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Foxoft.Ci {

  public class BeakInfo {
    public string Name;

    public BeakInfo(string aName) {
      this.Name = aName;
    }
  }

  public class GenPas : DelegateGenerator {
    public GenPas(string aNamespace) : this() {
      SetNamespace(aNamespace);
    }

    public GenPas() : base() {
      Namespace = "ci";
      BlockCloseStr = "end";
      BlockOpenStr = "begin";
      BlockCloseCR = false;
      CheckParam = true;
      CommentContinueStr = "/// ";
      CommentBeginStr = "";
      CommentEndStr = "";
      CommentCodeBegin = "@code(";
      CommentCodeEnd = ")";
      CommentListBegin = "@unorderedList(";
      CommentListEnd = ")";
      CommentItemListBegin = " @item";
      CommentItemListEnd = "";
      CommentSummaryBegin = "";
      CommentSummaryEnd = "";
      CommentRemarkBegin = "";
      CommentRemarkEnd = "";
      CommentSpecialChar.Add('\r', "");
      CommentSpecialChar.Add('{', "[");
      CommentSpecialChar.Add('}', "]");
      CommentSpecialChar.Add('(', "@(");
      CommentSpecialChar.Add(')', "@)");
      CommentSpecialChar.Add('@', "@@");
      Decode_TRUEVALUE = "true";
      Decode_FALSEVALUE = "false";
      Decode_STRINGBEGIN = "'";
      Decode_STRINGEND = "'";
      Decode_SPECIALCHAR.Clear();
      Decode_SPECIALCHAR.Add('\'', "\'\'");
      Decode_NONANSICHAR = "'#{1}'";
      Decode_ARRAYEND = " )";
      Decode_ARRAYBEGIN = "( ";
      promoteClassConst = true;
      TranslateSymbolName = Pas_SymbolNameTranslator;
    }

    public string Pas_SymbolNameTranslator(CiSymbol aSymbol) {
      String name = base.SymbolNameTranslator(aSymbol);
      if (aSymbol is CiClass) {
        name = "T" + name;
      }
      else if (aSymbol is CiClassType) {
        name = "T" + name;
      }
      return name;
    }

    #region Base Generator specialization
    public override void PreProcess(CiProgram prog) {
      ResetSwitch();
      base.PreProcess(prog);
    }

    public override string[] GetReservedWords() {
      return new String[] {
        "absolute",
        "and",
        "array",
        "as",
        "asm",
        "begin",
        "case",
        "class",
        "const",
        "constructor",
        "destructor",
        "dispinterface",
        "div",
        "do",
        "downto",
        "else",
        "end",
        "except",
        "exports",
        "file",
        "finalization",
        "finally",
        "for",
        "function",
        "goto",
        "if",
        "implementation",
        "in",
        "inherited",
        "initialization",
        "inline",
        "inline",
        "interface",
        "is",
        "label",
        "library",
        "mod",
        "nil",
        "not",
        "object",
        "of",
        "on",
        "operator",
        "or",
        "out",
        "packed",
        "procedure",
        "program",
        "property",
        "raise",
        "record",
        "reintroduce",
        "repeat",
        "resourcestring",
        "result",
        "self",
        "set",
        "shl",
        "shr",
        "string",
        "then",
        "threadvar",
        "to",
        "try",
        "type",
        "unit",
        "until",
        "uses",
        "var",
        "while",
        "with",
        "xor",
        "length"
      };
    }

    protected override void WriteBanner() {
      WriteLine("(* Generated automatically with \"cito\". Do not edit. *)");
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

    public StringBuilder oldLine = null;
    private static char[] trimmedChar = new char[] { '\r', '\t', '\n', ' ' };
    private static string trimmedString = new string(trimmedChar);

    protected override void WriteLine() {
      string newTxt = curLine.ToString().TrimEnd(trimmedChar);
      if (curLine.Equals("")) {
        oldLine.Append(NewLineStr);
        return;
      }
      string oldTxt = oldLine.ToString();
      if (newTxt.StartsWith("else")) {
        for (int i = oldTxt.Length - 1; i >= 0; i--) {
          if (trimmedString.IndexOf(oldTxt[i]) < 0) {
            if (oldTxt[i] == ';') {
              oldTxt = oldTxt.Remove(i, 1);
              break;
            }
          }
        }
      }
      else if ((!IsIndented()) && (newTxt.StartsWith("end;"))) {
        //(pork)workaround
        for (int i = oldTxt.Length - 1; i >= 0; i--) {
          if (trimmedString.IndexOf(oldTxt[i]) < 0) {
            if (i >= 5) {
              if (oldTxt.Substring(i - 4, 5).Equals("exit;")) {
                oldTxt = oldTxt.Remove(i - 5, 6).TrimEnd(trimmedChar);
                break;
              }
            }
          }
        }
      }
      int oldLength = fullCode.Length;
      fullCode.Insert(Position, oldTxt);
      Position += fullCode.Length - oldLength;
      oldLine = new StringBuilder();
      AppendIndentStr(oldLine);
      oldLine.Append(newTxt);
      oldLine.Append(NewLineStr);
      curLine = new StringBuilder();
    }

    protected override void Open(TextWriter writer) {
      oldLine = new StringBuilder();
      base.Open(writer);
    }

    protected override void Flush() {
      if (oldLine.Length > 0) {
        int oldLength = fullCode.Length;
        fullCode.Insert(Position, oldLine);
        Position += fullCode.Length - oldLength;
        oldLine = new StringBuilder();
      }
      base.Flush();
    }

    protected override void Close() {
      Flush();
      base.Close();
    }

    public virtual bool CheckCode(ICiStatement[] code) {
      CiBreak brk = (code != null) ? code[code.Length - 1] as CiBreak : null;
      return Execute(code, s => ((s is CiBreak) && (s != brk)));
    }

    public override bool PreProcess(CiMethod method, ICiStatement stmt) {
      if (stmt is CiVar) {
        CiVar v = (CiVar)stmt;
        SymbolMapping parent = FindSymbol(method);
        string vName = TranslateSymbolName(v);
        // Look if local Ci var in already defined in Pascal procedure vars
        foreach (SymbolMapping item in parent.childs) {
          if (String.Compare(item.NewName, vName, true) == 0) {
            return false;
          }
        }
        AddSymbol(parent, v);
        AddType(v.Type);
      }
      else if (stmt is CiSwitch) {
        CiSwitch swith = (CiSwitch)stmt;
        bool needExit = false;
        foreach (CiCase kase in swith.Cases) {
          needExit = CheckCode(kase.Body);
          if (needExit) {
            break;
          }
        }
        if (!needExit) {
          needExit = CheckCode(swith.DefaultBody);
        }
        if (needExit) {
          AddSwitch(method, swith);
        }
      }
      return false;
    }

    #region Converter Types
    public TypeInfo Type_CiBoolType(CiType type) {
      if (type.ArrayLevel > 0) {
        throw new ArgumentException("Invalid level in type :" + type);
      }
      return new TypeInfo(type, "boolean", "false");
    }

    public TypeInfo Type_CiByteType(CiType type) {
      if (type.ArrayLevel > 0) {
        throw new ArgumentException("Invalid level in type :" + type);
      }
      return new TypeInfo(type, "byte", "0");
    }

    public TypeInfo Type_CiIntType(CiType type) {
      if (type.ArrayLevel > 0) {
        throw new ArgumentException("Invalid level in type :" + type);
      }
      return new TypeInfo(type, "integer", "0");
    }

    public TypeInfo Type_CiStringPtrType(CiType type) {
      UseFunction("Unit_StrUtils");
      if (type.ArrayLevel > 0) {
        throw new ArgumentException("Invalid level in type :" + type);
      }
      return new TypeInfo(type, "string", "''");
    }

    public TypeInfo Type_CiStringStorageType(CiType type) {
      UseFunction("Unit_StrUtils");
      if (type.ArrayLevel > 0) {
        throw new ArgumentException("Invalid level in type :" + type);
      }
      return new TypeInfo(type, "string", "''");
    }

    public TypeInfo Type_CiClassPtrType(CiType type) {
      if (type.ArrayLevel > 0) {
        throw new ArgumentException("Invalid level in type :" + type);
      }
      return new TypeInfo(type, DecodeSymbol(type), "nil");
    }

    public TypeInfo Type_CiClassStorageType(CiType type) {
      if (type.ArrayLevel > 0) {
        throw new ArgumentException("Invalid level in type :" + type);
      }
      return new TypeInfo(type, DecodeSymbol(type), "nil");
    }

    public TypeInfo Type_CiEnum(CiType type) {
      if (type.ArrayLevel > 0) {
        throw new ArgumentException("Invalid level in type :" + type);
      }
      var ev = ((CiEnum)type).Values[0];
      TypeInfo info = new TypeInfo();
      info.Type = type;
      info.IsNative = true;
      info.NewType = type.Name;
      info.Definition = type.Name;
      info.Null = type.Name + "." + ev.Name;
      info.ItemType = info.NewType;
      info.ItemDefault = info.Null;
      return info;
    }

    public TypeInfo Type_CiArrayPtrType(CiType type) {
      if (type.ArrayLevel <= 0) {
        throw new ArgumentException("Invalid level in type :" + type);
      }
      return Type_CiArrayStorageType(type);
    }

    public TypeInfo Type_CiArrayStorageType(CiType type) {
      if (type.ArrayLevel <= 0) {
        throw new ArgumentException("Invalid level in type :" + type);
      }
      TypeInfo info = new TypeInfo();
      info.Type = type;
      info.IsNative = true;
      StringBuilder newType = new StringBuilder();
      StringBuilder def = new StringBuilder();
      StringBuilder nul = new StringBuilder();
      StringBuilder nulInit = new StringBuilder();
      StringBuilder init = new StringBuilder();
      TypeInfo baseType = GetTypeInfo(type.BaseType);
      nul.Append("EMPTY_");
      nulInit.Append("SetLength({3}");
      init.Append("SetLength([0]");
      for (int i = 0; i < type.ArrayLevel; i++) {
        def.Append("array of ");
        newType.Append("ArrayOf_");
        nul.Append("ArrayOf_");
        nulInit.Append(", 0");
        init.Append(", [" + (i + 1) + "]");
      }
      info.IsNative = false;
      nulInit.Append(")");
      init.Append(")");
      newType.Append(baseType.NewType);
      def.Append(baseType.NewType);
      nul.Append(baseType.NewType);
      info.ItemType = baseType.NewType;
      info.ItemDefault = baseType.Null;
      info.NewType = newType.ToString();
      info.Definition = def.ToString();
      info.Null = nul.ToString();
      info.NullInit = (nulInit.Length > 0 ? String.Format(nulInit.ToString(), info.NewType, info.Definition, info.ItemType, info.Null).Replace('[', '{').Replace(']', '}') : null);
      info.Init = (nulInit.Length > 0 ? String.Format(init.ToString(), info.NewType, info.Definition, info.ItemType, info.Null) : null);
      if (!IsReservedWord(info.Null)) {
        ReservedWords.Add(info.Null);
      }
      return info;
    }
    #endregion

    public override void InitOperators() {
      BinaryOperators.Declare(CiToken.Plus, CiPriority.Additive, true, ConvertOperator, " + ");
      BinaryOperators.Declare(CiToken.Minus, CiPriority.Additive, false, ConvertOperator, " - ");
      BinaryOperators.Declare(CiToken.Asterisk, CiPriority.Multiplicative, true, ConvertOperator, " * ");
      BinaryOperators.Declare(CiToken.Slash, CiPriority.Multiplicative, false, ConvertOperatorSlash, null);
      BinaryOperators.Declare(CiToken.Mod, CiPriority.Multiplicative, false, ConvertOperator, " mod ");
      BinaryOperators.Declare(CiToken.ShiftLeft, CiPriority.Multiplicative, false, ConvertOperator, " shl ");
      BinaryOperators.Declare(CiToken.ShiftRight, CiPriority.Multiplicative, false, ConvertOperator, " shr ");
      //
      BinaryOperators.Declare(CiToken.Equal, CiPriority.Equality, false, ConvertOperator, " = ");
      BinaryOperators.Declare(CiToken.NotEqual, CiPriority.Equality, false, ConvertOperator, " <> ");
      BinaryOperators.Declare(CiToken.Less, CiPriority.Equality, false, ConvertOperator, " < ");
      BinaryOperators.Declare(CiToken.LessOrEqual, CiPriority.Equality, false, ConvertOperator, " <= ");
      BinaryOperators.Declare(CiToken.Greater, CiPriority.Equality, false, ConvertOperator, " > ");
      BinaryOperators.Declare(CiToken.GreaterOrEqual, CiPriority.Equality, false, ConvertOperator, " >= ");
      BinaryOperators.Declare(CiToken.CondAnd, CiPriority.Equality, false, ConvertOperator, " and ");
      BinaryOperators.Declare(CiToken.CondOr, CiPriority.Equality, false, ConvertOperator, " or ");
      //
      BinaryOperators.Declare(CiToken.And, CiPriority.Multiplicative, true, ConvertOperator, " and ");
      BinaryOperators.Declare(CiToken.Or, CiPriority.Additive, true, ConvertOperator, " or ");
      BinaryOperators.Declare(CiToken.Xor, CiPriority.Additive, true, ConvertOperator, " xor ");
//
      UnaryOperators.Declare(CiToken.Increment, CiPriority.Prefix, ConvertOperatorInc, "__CINC_Pre(", ")");
      UnaryOperators.Declare(CiToken.Decrement, CiPriority.Prefix, ConvertOperatorDec, "__CDEC_Pre(", ")");
      UnaryOperators.Declare(CiToken.Minus, CiPriority.Prefix, ConvertOperatorUnary, "-(", ")");
      UnaryOperators.Declare(CiToken.Not, CiPriority.Prefix, ConvertOperatorUnary, "not (", ")");
    }

    public void ConvertOperatorInc(CiUnaryExpr expr, UnaryOperatorInfo token) {
      UseFunction("__CINC_Pre");
      ConvertOperatorUnary(expr, token);
    }

    public void ConvertOperatorDec(CiUnaryExpr expr, UnaryOperatorInfo token) {
      UseFunction("__CDEC_Pre");
      ConvertOperatorUnary(expr, token);
    }

    public void ConvertOperatorSlash(CiBinaryExpr expr, BinaryOperatorInfo token) {
      // Work-around to have correct left and right type
      GetExprType(expr);
      WriteChild(expr, expr.Left);
      CiType type = GetExprType(expr.Left);
      Write(DecodeDivSymbol(type));
      WriteChild(expr, expr.Right, true);
    }
    #endregion

    #region Converter Expression
    public void Expression_CiConstExpr(CiExpr expression) {
      CiConstExpr expr = (CiConstExpr)expression;
      Write(DecodeValue(GetExprType(expr), expr.Value));
    }

    public void Expression_CiConstAccess(CiExpr expression) {
      CiConstAccess expr = (CiConstAccess)expression;
      Write(DecodeSymbol(expr.Const));
    }

    public void Expression_CiVarAccess(CiExpr expression) {
      CiVarAccess expr = (CiVarAccess)expression;
      string name = expr.Var.Name;
      if (name.Equals("this")) {
        Write("Self");
      }
      else {
        Write(DecodeSymbol(expr.Var));
      }
    }

    public void Expression_CiFieldAccess(CiExpr expression) {
      CiFieldAccess expr = (CiFieldAccess)expression;
      WriteChild(expr, expr.Obj);
      Write('.');
      Write(DecodeSymbol(expr.Field));
    }

    public void Expression_CiPropertyAccess(CiExpr expression) {
      CiPropertyAccess expr = (CiPropertyAccess)expression;
      if (!Translate(expr)) {
        throw new ArgumentException(expr.Property.Name);
      }
    }

    public void Expression_CiArrayAccess(CiExpr expression) {
      CiArrayAccess expr = (CiArrayAccess)expression;
      WriteChild(expr, expr.Array);
      Write('[');
      Translate(expr.Index);
      Write(']');
    }

    public void Expression_CiMethodCall(CiExpr expression) {
      CiMethodCall expr = (CiMethodCall)expression;
      if (!Translate(expr)) {
        WriteMethodCall(expr);
      }
    }

    public void Expression_CiBinaryResourceExpr(CiExpr expression) {
      CiBinaryResourceExpr expr = (CiBinaryResourceExpr)expression;
      Write(DecodeSymbol(expr.Resource));
    }

    public void Expression_CiNewExpr(CiExpr expression) {
      CiNewExpr expr = (CiNewExpr)expression;
      if (expr.NewType is CiClassStorageType) {
        CiClassStorageType classType = (CiClassStorageType)expr.NewType;
        Write(DecodeSymbol(classType.Class));
        WriteLine(".Create();");
      }
      else if (expr.NewType is CiArrayStorageType) {
        WriteLine("Not_able_to_create_Array_Storage");
      }
    }

    public void Expression_CiUnaryExpr(CiExpr expression) {
      CiUnaryExpr expr = (CiUnaryExpr)expression;
      UnaryOperatorInfo tokenInfo = UnaryOperators.GetUnaryOperator(expr.Op);
      tokenInfo.WriteDelegate(expr, tokenInfo);
    }

    public void Expression_CiCondNotExpr(CiExpr expression) {
      CiCondNotExpr expr = (CiCondNotExpr)expression;
      Write("not (");
      WriteChild(expr, expr.Inner);
      Write(")");
    }

    public void Expression_CiPostfixExpr(CiExpr expression) {
      CiPostfixExpr expr = (CiPostfixExpr)expression;
      switch (expr.Op) {
        case CiToken.Increment:
          UseFunction("__CINC_Post");
          Write("__CINC_Post(");
          break;
        case CiToken.Decrement:
          UseFunction("__CDEC_Post");
          Write("__CDEC_Post(");
          break;
        default:
          throw new ArgumentException(expr.Op.ToString());
      }
      WriteChild(expr, expr.Inner);
      Write(")");
    }

    public void Expression_CiPostfixStatement(CiExpr expression) {
      CiPostfixExpr expr = (CiPostfixExpr)expression;
      switch (expr.Op) {
        case CiToken.Increment:
          Write("inc(");
          break;
        case CiToken.Decrement:
          Write("dec(");
          break;
        default:
          throw new ArgumentException(expr.Op.ToString());
      }
      WriteChild(expr, expr.Inner);
      Write(")");
    }

    public void Expression_CiCondExpr(CiExpr expression) {
      CiCondExpr expr = (CiCondExpr)expression;
      UseFunction("Unit_Math");
      Write("IfThen(");
      WriteChild(expr, expr.Cond, true);
      Write(", ");
      WriteCondChild(expr, expr.OnTrue);
      Write(", ");
      WriteCondChild(expr, expr.OnFalse);
      Write(")");
    }

    public void Expression_CiBinaryExpr(CiExpr expression) {
      CiBinaryExpr expr = (CiBinaryExpr)expression;
      BinaryOperatorInfo tokenInfo = BinaryOperators.GetBinaryOperator(expr.Op);
      tokenInfo.WriteDelegate(expr, tokenInfo);
    }

    public void Expression_CiCoercion(CiExpr expression) {
      CiCoercion expr = (CiCoercion)expression;
      if (expr.ResultType == CiByteType.Value && expr.Inner.Type == CiIntType.Value) {
        //      Write("byte(");
        WriteChild(expr, (CiExpr)expr.Inner); 
        //      Write(")");
      }
      else {
        WriteInline(expr.Inner);
      }
    }
    #endregion

    #region Converter Symbols
    public void Symbol_CiEnum(CiSymbol symbol) {
      CiEnum enu = (CiEnum)symbol;
      WriteLine();
      WriteDocCode(enu.Documentation);
      Write(DecodeSymbol(enu));
      WriteLine(" = (");
      OpenBlock(false);
      bool first = true;
      foreach (CiEnumValue value in enu.Values) {
        if (first) {
          first = false;
        }
        else {
          WriteLine(",");
        }
        WriteDocCode(value.Documentation);
        Write(DecodeSymbol(value));
      }
      WriteLine();
      CloseBlock(false);
      WriteLine(");");
    }

    public void Symbol_CiConst(CiSymbol symbol) {
      CiConst konst = (CiConst)symbol;
      WriteDocCode(konst.Documentation);
      Write("public ");
      if (!(konst.Type is CiArrayType)) {
        Write("const ");
      }
      WriteFormat("{0}: {1}", DecodeSymbol(konst), DecodeType(konst.Type));
      if (!(konst.Type is CiArrayType)) {
        WriteFormat(" = {0}", DecodeValue(konst.Type, konst.Value));
      }
      WriteLine(";");
    }

    public void Symbol_CiField(CiSymbol symbol) {
      WriteField((CiField)symbol, null, true);
    }
    #endregion

    #region Converter Statements
    public void Statement_CiBlock(ICiStatement statement) {
      CiBlock block = (CiBlock)statement;
      OpenBlock();
      WriteCode(block.Statements);
      CloseBlock();
    }

    public void Statement_CiVar(ICiStatement statement) {
      CiVar vr = (CiVar)statement;
      WriteInitVal(vr);
    }

    public void Statement_CiExpr(ICiStatement statement) {
      if (statement is CiPostfixExpr) {
        Expression_CiPostfixStatement((CiPostfixExpr)statement);
      }
      else {
        Translate((CiExpr)statement);
      }
    }

    public void Statement_CiAssign(ICiStatement statement) {
      CiAssign assign = (CiAssign)statement;
      if (assign.Source is CiAssign) {
        CiAssign src = (CiAssign)assign.Source;
        Statement_CiAssign(src);
        WriteLine(";");
        WriteAssign(assign.Target, assign.Op, src.Target);
      }
      else {
        WriteAssign(assign.Target, assign.Op, assign.Source);
      }
    }

    public void Statement_CiDelete(ICiStatement statement) {
      CiDelete stmt = (CiDelete)statement;
      CiTypedSymbol vr = null;
      if (stmt.Expr is CiVarAccess) {
        vr = ((CiVarAccess)stmt.Expr).Var;
      }
      else if (stmt.Expr is CiFieldAccess) {
        vr = ((CiFieldAccess)stmt.Expr).Field;
      }
      if (vr != null) {
        if (vr.Type is CiClassStorageType) {
          WriteFormat("FreeAndNil({0})", DecodeSymbol(vr));
        }
        else if (vr.Type is CiClassPtrType) {
          WriteFormat("FreeAndNil({0})", DecodeSymbol(vr));
        }
        else if (vr.Type is CiArrayStorageType) {
          TypeInfo info = GetTypeInfo(vr.Type);
          WriteFormat("{0}:= {1}", DecodeSymbol(vr), info.Null);
        }
        else if (vr.Type is CiArrayPtrType) {
          TypeInfo info = GetTypeInfo(vr.Type);
          WriteFormat("{0}:= {1}", DecodeSymbol(vr), info.Null);
        }
      }
    }

    public void Statement_CiBreak(ICiStatement statement) {
      BeakInfo label = CurrentBreakableBlock();
      if (label != null) {
        WriteLine("goto " + label.Name + ";");
      }
      else {
        WriteLine("break;");
      }
    }

    public void Statement_CiContinue(ICiStatement statement) {
      WriteLine("continue;");
    }

    public void Statement_CiDoWhile(ICiStatement statement) {
      CiDoWhile stmt = (CiDoWhile)statement;
      EnterBreakableBlock(stmt);
      WriteLine("repeat");
      WriteStatement(stmt.Body, true, true);
      Write("until not(");
      Translate(stmt.Cond);
      WriteLine(");");
      ExitBreakableBlock();
    }

    public void Statement_CiFor(ICiStatement statement) {
      CiFor stmt = (CiFor)statement;
      EnterBreakableBlock(stmt);
      bool hasInit = (stmt.Init != null);
      bool hasNext = (stmt.Advance != null);
      bool hasCond = (stmt.Cond != null);
      if (hasInit && hasNext && hasCond) {
        if (IsValidPascalFor(stmt)) {
          CiBoolBinaryExpr cond = (CiBoolBinaryExpr)stmt.Cond;
          CiPostfixExpr mode = (CiPostfixExpr)stmt.Advance;
          String dir = null;
          int lmt = 0;
          if (mode.Op == CiToken.Increment) {
            dir = " to ";
            if (cond.Op == CiToken.LessOrEqual) {
              lmt = 0;
            }
            else if (cond.Op == CiToken.Less) {
              lmt = -1;
            }
          }
          if (mode.Op == CiToken.Decrement) {
            dir = " downto ";
            if (cond.Op == CiToken.GreaterOrEqual) {
              lmt = 0;
            }
            else if (cond.Op == CiToken.Greater) {
              lmt = +1;
            }
          }
          if (dir != null) {
            CiVar var = (CiVar)stmt.Init;
            Write("for ");
            WriteInitVal(var);
            Write(dir);
            if ((cond.Right is CiConstExpr) && (((CiConstExpr)cond.Right).Value is Int32)) {
              Write((Int32)((CiConstExpr)cond.Right).Value + lmt);
            }
            else {
              Translate(cond.Right);
              if (lmt != 0) {
                if (lmt > 0) {
                  Write("+" + lmt);
                }
                else {
                  Write(lmt);
                }
              }
            }
            Write(" do ");
            WriteChild(stmt.Body);
            return;
          }
        }
      }
      if (hasInit) {
        Translate(stmt.Init);
        WriteLine(";");
      }
      Write("while (");
      if (hasCond) {
        Translate(stmt.Cond);
      }
      else {
        Write("true");
      }
      Write(") do ");
      if (hasNext) {
        OpenBlock();
        WriteStatement(stmt.Body, true, false);
        Translate(stmt.Advance);
        WriteLine(";");
        CloseBlock();
      }
      else {
        WriteChild(stmt.Body);
      }
      ExitBreakableBlock();
    }

    public void Statement_CiIf(ICiStatement statement) {
      CiIf stmt = (CiIf)statement;
      if (stmt.Cond is CiConstExpr) {
        CiConstExpr expr = (CiConstExpr)stmt.Cond;
        if (expr.Value is bool) {
          bool val = (bool)expr.Value;
          WriteLine("// Unecessary IF removed");
          if (val) {
            WriteStatement(stmt.OnTrue, true, false);
          }
          else {
            WriteStatement(stmt.OnFalse, true, false);
          }
          return;
        }
      }
      Write("if ");
      EnterContext(1);
      Translate(stmt.Cond);
      ExitContext();
      Write(" then ");
      WriteChild(stmt.OnTrue);
      if (stmt.OnFalse != null) {
        Write("else ");
        if (stmt.OnFalse is CiIf) {
          WriteCode(stmt.OnFalse);
        }
        else {
          WriteChild(stmt.OnFalse);
        }
      }
    }

    public void Statement_CiNativeBlock(ICiStatement statement) {
      CiNativeBlock block = (CiNativeBlock)statement;
      Write(block.Content);
    }

    public void Statement_CiReturn(ICiStatement statement) {
      CiReturn stmt = (CiReturn)statement;
      if (stmt.Value == null) {
        Write("exit");
      }
      else {
        EnterContext(1);
        if (stmt.Value is CiCondExpr) {
          CiCondExpr expr = (CiCondExpr)stmt.Value;
          Write("if ");
          WriteChild(expr, expr.Cond, true);
          Write(" then ");
          Write("Result:= ");
          WriteCondChild(expr, expr.OnTrue);
          Write(" else ");
          Write("Result:= ");
          WriteCondChild(expr, expr.OnFalse);
          WriteLine(";");
        }
        else if (stmt.Value is CiNewExpr) {
          CiVar result = new CiVar();
          result.Name = "Result";
          WriteInitNew(result, ((CiNewExpr)stmt.Value).NewType);
          WriteLine(";");
        }
        else {
          Write("Result:= ");
          if (stmt.Value is CiConstExpr) {
            CiMethod call = CurrentMethod();
            CiType type = (call != null ? call.Signature.ReturnType : null);
            Write(DecodeValue(type, ((CiConstExpr)stmt.Value).Value));
          }
          else {
            Translate(stmt.Value);
          }
          WriteLine(";");
        }
        Write("exit");
        ExitContext();
      }
    }

    public void Statement_CiSwitch(ICiStatement statement) {
      CiSwitch swich = (CiSwitch)statement;
      BeakInfo label = EnterBreakableBlock(swich);
      Write("case (");
      Translate(swich.Value);
      WriteLine(") of");
      OpenBlock(false);
      foreach (CiCase kase in swich.Cases) {
        bool first = true;
        foreach (object value in kase.Values) {
          if (!first) {
            Write(", ");
          }
          else {
            first = false;
          }
          Write(DecodeValue(null, value));
        }
        Write(": ");
        WriteCaseInternal(swich, kase, kase.Body, null);
        WriteLine(";");
      }
      if (WriteCaseInternal(swich, null, swich.DefaultBody, "else ")) {
        WriteLine(";");
      }
      CloseBlock(false);
      WriteLine("end;");
      ExitBreakableBlock();
      if (label != null) {
        WriteLine(label.Name + ": ;");
      }
    }

    public void Statement_CiThrow(ICiStatement statement) {
      CiThrow stmt = (CiThrow)statement;
      Write("Raise Exception.Create(");
      Translate(stmt.Message);
      WriteLine(");");
    }

    public void Statement_CiWhile(ICiStatement statement) {
      CiWhile stmt = (CiWhile)statement;
      EnterBreakableBlock(stmt);
      Write("while (");
      Translate(stmt.Cond);
      Write(") do ");
      WriteChild(stmt.Body);
      ExitBreakableBlock();
    }
    #endregion

    #region CiTo Library handlers
    public void Library_SByte(CiPropertyAccess expr) {
      Write("shortint(");
      WriteChild(expr, expr.Obj);
      Write(")");
    }

    public void Library_LowByte(CiPropertyAccess expr) {
      Write("byte(");
      WriteChild(expr, expr.Obj);
      Write(")");
    }

    public void Library_Length(CiPropertyAccess expr) {
      Write("Length(");
      WriteChild(expr, expr.Obj);
      Write(")");
    }

    public void Library_MulDiv(CiMethodCall expr) {
      Write("(int64(");
      WriteChild(CiPriority.Prefix, expr.Obj);
      Write(") * int64(");
      WriteChild(CiPriority.Multiplicative, expr.Arguments[0]);
      Write(") div ");
      WriteChild(CiPriority.Multiplicative, expr.Arguments[1], true);
      Write(")");
    }

    public void Library_CharAt(CiMethodCall expr) {
      Write("ord(");
      Translate(expr.Obj);
      Write("[");
      Translate(expr.Arguments[0]);
      Write("+1])");
    }

    public void Library_Substring(CiMethodCall expr) {
      Write("MidStr(");
      Translate(expr.Obj);
      Write(", ");
      Translate(expr.Arguments[0]);
      Write("+1, ");
      Translate(expr.Arguments[1]);
      Write(")");
    }

    public void Library_CopyTo(CiMethodCall expr) {
      Write("__CCOPY(");
      Translate(expr.Obj);
      Write(", ");
      Translate(expr.Arguments[0]);
      Write(", ");
      Translate(expr.Arguments[1]);
      Write(", ");
      Translate(expr.Arguments[2]);
      Write(", ");
      Translate(expr.Arguments[3]);
      Write(')');
    }

    public void Library_ToString(CiMethodCall expr) {
      UseFunction("__TOSTR");
      Write("__TOSTR(");
      Translate(expr.Obj);
      Write(", ");
      Translate(expr.Arguments[0]);
      Write(", ");
      Translate(expr.Arguments[1]);
      Write(")");
    }

    public void Library_Clear(CiMethodCall expr) {
      Write("__CCLEAR(");
      Translate(expr.Obj);
      Write(")");
    }
    #endregion

    // Emit pascal program
    public override void EmitProgram(CiProgram prog) {
      CreateFile(this.OutputFile);
      // Prologue
      WriteLine("unit " + (!string.IsNullOrEmpty(this.Namespace) ? this.Namespace : "cito") + ";");
      // Declaration
      EmitInterfaceHeader();
      if (!HasSymbols()) {
        WriteLine();
        WriteLine("type");
        OpenBlock(false);
        EmitEnums(prog);
        EmitSuperTypes();
        EmitClassesInterface(prog);
        CloseBlock(false);
      }
      if (promoteClassConst) {
        EmitConstants(prog);
      }
      // Implementation
      EmitImplementationHeader();
      EmitClassesImplementation();
      //Epilogue
      EmitInitialization(prog);
      WriteLine("end.");
      //Insert used Helper functions
      EmitHelperFunctions();
      EmitUses();
      CloseFile();
    }

    PositionMark UsesMark;

    public void EmitInterfaceHeader() {
      WriteLine();
      WriteLine("interface");
      WriteLine();
      UsesMark = GetMark();
    }

    public void EmitUses() {
      SetMark(UsesMark);
      StringBuilder uses = new StringBuilder();
      uses.Append("uses SysUtils");
      if (IsUsedFunction("Unit_StrUtils")) {
        uses.Append(", StrUtils");
      }
      if (IsUsedFunction("Unit_Classes")) {
        uses.Append(", Classes");
      }
      if (IsUsedFunction("Unit_Math")) {
        uses.Append(", Math");
      }
      uses.Append(";");
      WriteLine(uses.ToString());
      SetMark(null);
    }

    public void EmitEnums(CiProgram prog) {
      foreach (CiSymbol symbol in prog.Globals) {
        if (symbol is CiEnum) {
          Symbol_CiEnum(symbol);
        }
      }
    }

    public void EmitSuperTypes() {
      bool sep = false;
      foreach (CiClass klass in GetClassTypeList()) {
        if (!sep) {
          WriteLine();
          sep = true;
        }
        WriteLine("{0} = class;", DecodeSymbol(klass));
      }
      HashSet<string> types = new HashSet<string>();
      foreach (CiType t in GetTypeList()) {
        TypeInfo info = GetTypeInfo(t);
        if (!info.IsNative) {
          if (sep) {
            sep = false;
            WriteLine();
          }
          if (!types.Contains(info.NewType)) {
            types.Add(info.NewType);
            WriteLine("{0} = {1};", info.NewType, info.Definition);
          }
        }
      }
    }

    public void EmitClassesInterface(CiProgram prog) {
      UseFunction("Unit_Classes");
      var delegates = prog.Globals.Where(s => s is CiDelegate);
      if (delegates.Count() > 0) {
        WriteLine();
        foreach (CiDelegate del in delegates) {
          WriteDocCode(del.Documentation);
          WriteSignature(null, del, true);
          WriteLine(";");
        }
      }
      foreach (CiClass klass in GetOrderedClassList()) {
        EmitClassInterface(klass);
      }
    }

    public void EmitClassInterface(CiClass klass) {
      WriteLine();
      WriteDocCode(klass.Documentation);
      string baseType = (klass.BaseClass != null) ? DecodeSymbol(klass.BaseClass) : "TInterfacedObject";
      WriteLine("{0} = class({1})", DecodeSymbol(klass), baseType);
      OpenBlock(false);
      foreach (CiSymbol member in klass.Members) {
        if (!(member is CiMethod)) {
          if ((member is CiConst) && promoteClassConst) {
            continue;
          } 
          Translate(member);
        }
      }
      if (!promoteClassConst) {
        foreach (CiSymbol member in klass.Members) {
          if ((member is CiMethod)) {
            Execute(((CiMethod)member).Body, s => {
              if (s is CiConst) {
                Symbol_CiConst((CiConst)s);
              }
              return false;
            });
          }
        }
      }
      WriteLine("public constructor Create;");
      foreach (CiSymbol member in klass.Members) {
        if ((member is CiMethod)) {
          WriteMethodIntf((CiMethod)member);
        }
      }
      CloseBlock(false);
      WriteLine("end;");
    }

    protected PositionMark HelperMark;

    public void EmitImplementationHeader() {
      WriteLine();
      WriteLine("implementation");
      WriteLine();
      bool first = true;
      HashSet<string> types = new HashSet<string>();
      foreach (CiType t in GetTypeList()) {
        TypeInfo info = GetTypeInfo(t);
        if (!info.IsNative) {
          if (first) {
            first = false;
          }
          if (!types.Contains(info.NewType)) {
            types.Add(info.NewType);
            WriteLine("var {0}: {1};", info.Null, info.NewType);
            WriteLine("procedure __CCLEAR(var x: {0}); overload; var i: integer; begin for i:= low(x) to high(x) do x[i]:= {1}; end;", info.NewType, info.ItemDefault);
            WriteLine("procedure __CFILL (var x: {0}; v: {1}); overload; var i: integer; begin for i:= low(x) to high(x) do x[i]:= v; end;", info.NewType, info.ItemType);
            WriteLine("procedure __CCOPY (const source: {0}; sourceStart: integer; var dest: {0}; destStart: integer; len: integer); overload; var i: integer; begin for i:= 0 to len do dest[i+destStart]:= source[i+sourceStart]; end;", info.NewType);
          }
        }
      }
      HelperMark = GetMark();
    }

    public void EmitHelperFunctions() {
      SetMark(HelperMark);
      if (IsUsedFunction("__CDEC_Pre")) {
        WriteLine("function  __CDEC_Pre (var x: integer): integer; overload; inline; begin dec(x); Result:= x; end;");
        WriteLine("function  __CDEC_Pre (var x: byte): byte; overload; inline; begin dec(x); Result:= x; end;");
      }
      if (IsUsedFunction("__CDEC_Post")) {
        WriteLine("function  __CDEC_Post(var x: integer): integer; overload; inline; begin Result:= x; dec(x); end;");
        WriteLine("function  __CDEC_Post(var x: byte): byte; overload; inline; begin Result:= x; dec(x); end;");
      }
      if (IsUsedFunction("__CINC_Pre")) {
        WriteLine("function  __CINC_Pre (var x: integer): integer; overload; inline; begin inc(x); Result:= x; end;");
        WriteLine("function  __CINC_Pre (var x: byte): byte; overload; inline; begin inc(x); Result:= x; end;");
      }
      if (IsUsedFunction("__CINC_Post")) {
        WriteLine("function  __CINC_Post(var x: integer): integer; overload; inline; begin Result:= x; inc(x); end;");
        WriteLine("function  __CINC_Post(var x: byte): byte; overload; inline; begin Result:= x; inc(x); end;");
      }
      if (IsUsedFunction("__getMagic")) {
        WriteLine("function  __getMagic(const cond: array of boolean): integer; var i: integer; var o: integer; begin Result:= 0; for i:= low(cond) to high(cond) do begin if (cond[i]) then o:= 1 else o:= 0; Result:= Result shl 1 + o; end; end;");
      }
      if (IsUsedFunction("__getBinaryResource")) {
        WriteLine("function  __getBinaryResource(const aName: string): ArrayOf_byte; var myfile: TFileStream; begin myFile:= TFileStream.Create(aName, fmOpenRead); SetLength(Result, myFile.Size); try myFile.seek(0, soFromBeginning); myFile.ReadBuffer(Result, myFile.Size); finally myFile.free; end; end;");
      }
      if (IsUsedFunction("__TOSTR")) {
        WriteLine("function  __TOSTR (const x: ArrayOf_byte; sourceIndex: integer; len: integer): string; var i: integer; begin Result:= ''; for i:= sourceIndex to sourceIndex+len do Result:= Result + chr(x[i]); end;");
      }
      SetMark(null);
    }

    public void EmitConstants(CiProgram prog) {
      bool first = true;
      foreach (CiSymbol symbol in prog.Globals) {
        if (symbol is CiClass) {
          CiClass klass = (CiClass)symbol;
          foreach (CiConst konst in klass.ConstArrays) {
            if (first) {
              WriteLine();
              WriteLine("var");
              OpenBlock(false);
              first = false;
            }
            WriteLine("{0}: {1};", DecodeSymbol(konst), DecodeType(konst.Type));

          }
        }
      }
      if (!first) {
        CloseBlock(false);
      }
      first = true;
      foreach (CiSymbol symbol in prog.Globals) {
        if (symbol is CiClass) {
          CiClass klass = (CiClass)symbol;
          foreach (CiBinaryResource resource in klass.BinaryResources) {
            if (first) {
              WriteLine();
              WriteLine("var");
              OpenBlock(false);
              first = false;
            }
            WriteLine("{0}: ArrayOf_byte;", DecodeSymbol(resource));
          }
        }
      }
      if (!first) {
        CloseBlock(false);
      }
    }

    public void EmitClassesImplementation() {
      foreach (CiClass klass in GetOrderedClassList()) {
        EmitClassImplementation(klass);
      }
    }

    public void EmitClassImplementation(CiClass klass) {
      WriteLine();
      WriteMethodCreateImpl(klass);
      foreach (CiSymbol member in klass.Members) {
        if (member is CiMethod) {
          CiMethod method = (CiMethod)member;
          if (method.CallType != CiCallType.Abstract) {
            WriteMethodImpl(method);
          }
        }
      }
    }

    public void EmitInitialization(CiProgram prog) {
      PositionMark mark = GetMark();
      bool hasInit = false;
      OpenBlock(false);
      HashSet<string> types = new HashSet<string>();
      foreach (CiType t in GetTypeList()) {
        TypeInfo info = GetTypeInfo(t);
        if (info.NullInit != null) {
          if (!types.Contains(info.NewType)) {
            types.Add(info.NewType);
            Write(info.NullInit);
            WriteLine(";");
            hasInit = true;
          }
        }
      }
      if (promoteClassConst) {
        foreach (CiSymbol symbol in prog.Globals) {
          if (symbol is CiClass) {
            CiClass klass = (CiClass)symbol;
            foreach (CiConst konst in klass.ConstArrays) {
              WriteConstFull(konst);
              hasInit = true;
            }
          }
        }
      }
      foreach (CiSymbol symbol in prog.Globals) {
        if (symbol is CiClass) {
          CiClass klass = (CiClass)symbol;
          foreach (CiBinaryResource resource in klass.BinaryResources) {
            UseFunction("__getBinaryResource");
            WriteLine("{0}:= __getBinaryResource('{1}');", DecodeSymbol(resource), resource.Name);
            hasInit = true;
          }
        }
      }
      CloseBlock(false);
      if (hasInit) {
        SetMark(mark);
        WriteLine();
        WriteLine("initialization");
        SetMark(null);
      }
    }

    string DecodeDivSymbol(CiType type) {
      return ((type is CiIntType) || (type is CiByteType)) ? " div " : " / ";
    }

    string DecodeVisibility(CiVisibility visibility) {
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
          res = "?";
          break;
      }
      return res;
    }

    bool IsChanging(CiExpr expr, CiVar v4r) {
      CiVar v = null;
      if (expr is CiBoolBinaryExpr) {
        if (((CiBoolBinaryExpr)expr).Left is CiUnaryExpr) {
          var stmt = ((CiUnaryExpr)(((CiBoolBinaryExpr)expr).Left)).Inner;
          if (stmt is CiVarAccess) {
            v = ((CiVarAccess)stmt).Var;
          }
        }
      }
      return (v != null) ? (v == v4r) : false;
    }

    bool IsAssignmentOf(ICiStatement stmt, CiVar v4r) {
      CiVar v = null;
      if (stmt is CiPostfixExpr) {
        if (((CiPostfixExpr)stmt).Inner is CiVarAccess) {
          v = (CiVar)((CiVarAccess)(((CiPostfixExpr)stmt).Inner)).Var;
        }
      }
      else if (stmt is CiVarAccess) {
        v = (CiVar)((CiVarAccess)stmt).Var;
      }
      else if (stmt is CiAssign) {
        if (((CiAssign)stmt).Target is CiVarAccess) {
          v = (CiVar)((CiVarAccess)(((CiAssign)stmt).Target)).Var;
        }
      }
      return (v != null) ? (v == v4r) : false;
    }
    // Detect Pascal For loop
    bool IsValidPascalFor(CiFor stmt) {
      if (!(stmt.Init is CiVar)) {
        return false;
      }
      // Single variable
      var loopVar = (CiVar)stmt.Init;
      if (loopVar.InitialValue is CiCondExpr) {
        return false;
      }
      // Step must be variable (de|in)cremented
      if ((stmt.Advance is CiPostfixExpr) && (stmt.Cond is CiBoolBinaryExpr)) {
        if (!IsAssignmentOf(stmt.Advance, loopVar)) {
          return false;
        }
        CiBoolBinaryExpr cond = (CiBoolBinaryExpr)stmt.Cond;
        // bounded by const or var
        if ((cond.Left is CiVarAccess) && ((cond.Right is CiConstExpr) || (cond.Right is CiVarAccess) || (cond.Right is CiFieldAccess))) {
          if (((CiVarAccess)cond.Left).Var == loopVar) {
            // loop variabale cannot be changed inside the loop
            if (Execute(stmt.Body, s => IsAssignmentOf(s, loopVar))) {
              return false;
            }
            if (Execute(stmt.Body, s => (s is CiLoop) && IsChanging(((CiLoop)s).Cond, loopVar))) {
              return false;
            }
            return true;
          }
        }
      }
      return false;
    }

    public void WriteStatement(ICiStatement stmt, bool removeBlock, bool Indent) {
      if (stmt == null) {
        return;
      }
      if (removeBlock && (stmt is CiBlock)) {
        if (Indent) {
          OpenBlock(false);
        }
        WriteCode(((CiBlock)stmt).Statements);
        if (Indent) {
          CloseBlock(false);
        }
      }
      else {
        WriteChild(stmt);
      }
    }

    void WriteCode(ICiStatement[] block) {
      foreach (ICiStatement stmt in block) {
        WriteCode(stmt);
      }
    }

    void WriteCode(ICiStatement stmt) {
      if (stmt == null) {
        return;
      }
      Translate(stmt);
      if (curLine.Length > 0) {
        WriteLine(";");
      }
    }

    void WriteChild(ICiStatement stmt) {
      if (stmt is CiBlock) {
        WriteCode((CiBlock)stmt);
      }
      else {
        if ((stmt is CiReturn) && (((CiReturn)stmt).Value != null)) {
          OpenBlock();
          WriteCode(stmt);
          CloseBlock();
        }
        else {
          WriteCode(stmt);
        }
      }
    }

    void WriteMethodIntf(CiMethod method) {
      WriteDocCode(method.Documentation);
      foreach (CiParam param in method.Signature.Params) {
        if (param.Documentation != null) {
          WriteFormat("{1} @param '{0}' ", DecodeSymbol(param), CommentContinueStr);
          WriteDocPara(param.Documentation.Summary);
          WriteLine();
        }
      }
      WriteFormat("{0} ", DecodeVisibility(method.Visibility));
      if (method.CallType == CiCallType.Static) {
        Write("class ");
      }
      WriteSignature(null, method.Signature, false);
      switch (method.CallType) {
        case CiCallType.Abstract:
          Write("; virtual; abstract");
          break;
        case CiCallType.Virtual:
          Write("; virtual");
          break;
        case CiCallType.Override:
          Write("; override");
          break;
      }
      WriteLine(";");
    }

    void WriteMethodCreateImpl(CiClass klass) {
      WriteLine("constructor {0}.Create;", DecodeSymbol(klass));
      if (klass.Constructor != null) {
        WriteLabels(klass.Constructor);
        WriteVars(klass.Constructor);
      }
      OpenBlock();
      if (klass.BaseClass != null) {
        WriteLine("inherited;");
      }
      if (!promoteClassConst) {
        foreach (CiSymbol member in klass.Members) {
          if (member is CiConst) {
            var konst = (CiConst)member;
            if (konst.Type is CiArrayType) {
              WriteConstFull(konst);
            }
          }
          else if (member is CiMethod) {
            Execute(((CiMethod)member).Body, s => {
              if (s is CiConst) {
                WriteConstFull((CiConst)s);
              }
              return false;
            });
          }
        }
      }
      WriteVarsInit(klass);
      if (klass.Constructor != null) {
        WriteVarsInit(klass.Constructor);
        if (klass.Constructor.Body is CiBlock) {
          WriteCode(((CiBlock)klass.Constructor.Body).Statements);
        }
        else {
          WriteCode(klass.Constructor.Body);
        }
      }
      CloseBlock();
      WriteLine(";");
    }

    void WriteMethodImpl(CiMethod method) {
      EnterMethod(method);
      WriteLine();
      if (method.CallType == CiCallType.Static) {
        Write("class ");
      }
      WriteSignature(DecodeSymbol(method.Class), method.Signature, false);
      WriteLine(";");
      // Emit Variabiles
      WriteLabels(method);
      WriteVars(method);
      // Emit Code
      OpenBlock();
      WriteVarsInit(method);
      if (method.Body is CiBlock) {
        WriteCode(((CiBlock)method.Body).Statements);
      }
      else {
        WriteCode(method.Body);
      }
      CloseBlock();
      WriteLine(";");
      ExitMethod();
    }

    void WriteLabels(CiMethod method) {
      List<BeakInfo> labels = GetExitLabels(method);
      if (labels != null) {
        foreach (BeakInfo label in labels) {
          WriteLine("label " + label.Name + ";");
        }
      }
    }

    void WriteSignature(string prefix, CiDelegate del, bool typeDeclare) {
      if (typeDeclare) {
        Write(DecodeSymbol(del) + " = ");
      }
      if (del.ReturnType == CiType.Void) {
        Write("procedure ");
      }
      else {
        Write("function ");
      }
      if (!typeDeclare) {
        if (prefix != null) {
          Write(prefix + ".");
        }
        Write(DecodeSymbol(del));
      }
      Write('(');
      bool first = true;
      foreach (CiParam param in del.Params) {
        if (first) {
          first = false;
        }
        else {
          Write("; ");
        }
        if (param.Type is CiArrayType) {
          Write("var ");
        }
        else if (param.Type is CiStringType) {
          Write(""); // TODO should be var but constant propagration must be handled
        }
        WriteFormat("{0}: {1}", DecodeSymbol(param), DecodeType(param.Type));
      }
      Write(')');
      if (del.ReturnType != CiType.Void) {
        WriteFormat(": {0}", DecodeType(del.ReturnType));
      }
      if (typeDeclare) {
        if (prefix != null) {
          Write(" of object");
        }
      }
    }

    void WriteVars(CiSymbol symb) {
      SymbolMapping vars = FindSymbol(symb);
      if (vars != null) {
        foreach (SymbolMapping var in vars.childs) {
          if (var.Symbol == null) {
            continue;
          }
          if (var.Symbol is CiVar) {
            WriteVar((CiVar)var.Symbol, true);
          }
          if (var.Symbol is CiField) {
            WriteField((CiField)var.Symbol, var.NewName, true);
          }
        }
      }
    }

    void WriteVarsInit(CiSymbol symb) {
      SymbolMapping vars = FindSymbol(symb);
      if (vars != null) {
        foreach (SymbolMapping var in vars.childs) {
          if (var.Symbol == null) {
            continue;
          }
          if (var.Symbol is CiTypedSymbol) {
            CiTypedSymbol v = (CiTypedSymbol)var.Symbol;
            WriteInitNew(v, v.Type);
          }
        }
      }
    }

    void WriteField(CiField field, string NewName, bool docs) {
      if (docs) {
        WriteDocCode(field.Documentation);
      }
      WriteLine("{0} {1}: {2};", DecodeVisibility(field.Visibility), DecodeSymbol(field), DecodeType(field.Type));
    }

    void WriteVar(CiVar var, bool docs) {
      if (docs) {
        WriteDocCode(var.Documentation);
      }
      WriteLine("var {0}: {1};", DecodeSymbol(var), DecodeType(var.Type));
    }

    void WriteAssignNew(CiVar Target, CiType Type) {
      if (Type is CiClassStorageType) {
        CiClassStorageType classType = (CiClassStorageType)Type;
        WriteLine("{0}:= {1}.Create();", DecodeSymbol(Target), DecodeSymbol(classType.Class));
      }
      else if (Type is CiArrayStorageType) {
        CiArrayStorageType arrayType = (CiArrayStorageType)Type;
        WriteFormat("SetLength({0}, ", DecodeSymbol(Target));
        if (arrayType.LengthExpr != null) {
          Translate(arrayType.LengthExpr);
        }
        else {
          Write(arrayType.Length);
        }
        WriteLine(");");
      }
    }

    void WriteAssignNew(CiExpr Target, CiToken Op, CiType Type) {
      if (Op != CiToken.Assign) {
        throw new InvalidOperationException("Unsupported assigment");
      }
      if (Type is CiClassStorageType) {
        CiClassStorageType classType = (CiClassStorageType)Type;
        Translate(Target);
        WriteLine(":= {0}.Create();", DecodeSymbol(classType.Class));
      }
      else if (Type is CiArrayStorageType) {
        CiArrayStorageType arrayType = (CiArrayStorageType)Type;
        Write("SetLength(");
        Translate(Target);
        Write(", ");
        if (arrayType.LengthExpr != null) {
          Translate(arrayType.LengthExpr);
        }
        else {
          Write(arrayType.Length);
        }
        WriteLine(");");
      }
      else if (Type is CiArrayPtrType) {
        CiArrayStorageType arrayType = (CiArrayStorageType)Type;
        Write("SetLength(");
        Translate(Target);
        Write(", ");
        if (arrayType.LengthExpr != null) {
          Translate(arrayType.LengthExpr);
        }
        else {
          Write(arrayType.Length);
        }
        WriteLine(");");
      }
    }

    void WriteInitNew(CiSymbol symbol, CiType Type) {
      string prefix = "";
      if (symbol is CiField) {
        prefix = "Self.";
      }
      if (Type is CiClassStorageType) {
        CiClassStorageType classType = (CiClassStorageType)Type;
        WriteLine("{0}{1}:= {2}.Create();", prefix, DecodeSymbol(symbol), DecodeSymbol(classType.Class));
      }
      else if (Type is CiArrayStorageType) {
        CiArrayStorageType arrayType = (CiArrayStorageType)Type;
        WriteFormat("SetLength({0}{1}, ", prefix, DecodeSymbol(symbol));
        if (arrayType.LengthExpr != null) {
          Translate(arrayType.LengthExpr);
        }
        else {
          Write(arrayType.Length);
        }
        WriteLine(");");
      }
    }

    void WriteFillArray(CiTypedSymbol vr, CiExpr val) {
      if (ExprAsInteger(val, -1) == 0) {
        WriteFormat("__CCLEAR({0})", DecodeSymbol(vr));
      }
      else {
        WriteFormat("__CFILL({0}, ", DecodeSymbol(vr));
        Translate(val);
        Write(")");
      }
    }

    void WriteInitVal(CiVar vr) {
      if (vr.InitialValue != null) {
        if (vr.Type is CiArrayStorageType) {
          WriteFillArray(vr, vr.InitialValue);
        }
        else {
          WriteAssign(vr, vr.InitialValue);
        }
      }
    }

    void WriteConstFull(CiConst konst) {
      object value = konst.Value;
      if (value is Array) {
        CiType elemType = null;
        if (konst.Type is CiArrayStorageType) {
          CiArrayStorageType type = (CiArrayStorageType)konst.Type;
          elemType = type.ElementType;
        }
        Array array = (Array)value;
        WriteLine("SetLength({0}, {1});", DecodeSymbol(konst), array.Length);
        for (int i = 0; i < array.Length; i++) {
          WriteLine("{0}[{1}]:= {2};", DecodeSymbol(konst), i, DecodeValue(elemType, array.GetValue(i)));
        }
      }
      else {
        WriteLine("{0}:= {1};", DecodeSymbol(konst), DecodeValue(konst.Type, value));
      }
    }

    bool WriteCaseInternal(CiSwitch swich, CiCase kase, ICiStatement[] body, string prefix) {
      if (body == null) {
        return false;
      }
      bool hasStmt = body.Any(s => !(s is CiBreak));
      if (!hasStmt) {
        return false;
      }
      if (!String.IsNullOrEmpty(prefix)) {
        Write(prefix);
      }
      OpenBlock();
      CiBreak breakFound = null;
      foreach (ICiStatement bodyStmt in body) {
        if (breakFound != null) {
          WriteCode(breakFound);
          breakFound = null;
        }
        if (!(bodyStmt is CiBreak)) {
          WriteCode(bodyStmt);
        }
        else {
          breakFound = (CiBreak)bodyStmt;
        }
      }
      if ((kase != null) && (kase.Fallthrough)) {
        if (kase.FallthroughTo == null) {
          WriteCaseInternal(swich, null, swich.DefaultBody, null);
        }
        else {
          if (kase.FallthroughTo is CiConstExpr) {
            string e = ((CiConstExpr)kase.FallthroughTo).Value.ToString();
            bool stop = false;
            foreach (var kkase in swich.Cases) {
              foreach (var val in kkase.Values) {
                if (val.ToString().Equals(e)) {
                  WriteLine("// include case " + val);
                  WriteCaseInternal(swich, kkase, kkase.Body, null);
                  WriteLine(";");
                  stop = true;
                  break;
                }
                if (stop) {
                  break;
                }
              }
            }
          }
          else {
            throw new InvalidOperationException("Unsupported fallthrough");
          }
        }
      }
      CloseBlock();
      return true;
    }

    void WriteArguments(CiMethodCall expr, bool[] conds) {
      Write('(');
      int cond = 0;
      for (int i = 0; i < expr.Arguments.Length; i++) {
        CiExpr arg = expr.Arguments[i];
        CiParam prm = expr.Signature.Params[i];
        if (i > 0) {
          Write(", ");
        }
        if ((arg is CiCondExpr) && (conds != null)) {
          if (conds[cond]) {
            WriteExpr(prm.Type, ((CiCondExpr)arg).OnTrue);
          }
          else {
            WriteExpr(prm.Type, ((CiCondExpr)arg).OnFalse);
          }
          cond++;
        }
        else {
          WriteExpr(prm.Type, arg);
        }
      }
      Write(')');
    }

    void WriteDelegateCall(CiExpr expr) {
      Translate(expr);
    }

    void WriteMethodCall2(CiMethodCall expr, bool[] cond) {
      if (expr.Method != null) {
        if (expr.Obj != null) {
          Translate(expr.Obj);
        }
        else {
          Write(DecodeSymbol(expr.Method.Class));
        }
        WriteFormat(".{0}", DecodeSymbol(expr.Method));
      }
      else {
        WriteDelegateCall(expr.Obj);
      }
      WriteArguments(expr, cond);
    }

    void BuildCond(int cond, CiMethodCall expr, int level) {
      int mask = 1;
      bool[] leaf = new bool[level];
      for (int i = 0; i < level; i++, mask *= 2) {
        leaf[i] = ((cond & mask) == mask);
      }
      WriteMethodCall2(expr, leaf);
    }

    void WriteMethodCall(CiMethodCall expr) {
      bool processed = false;
      if (expr.Arguments.Length > 0) {
        if (!InContext(1)) {
          List<CiExpr> iifs = expr.Arguments.Where(arg => arg is CiCondExpr).ToList();
          int level = iifs.Count;
          if (level > 0) {
            if (level > 1) {
              UseFunction("__getMagic");
              Write("case (__getMagic([");
              for (int i = 0; i < level; i++) {
                if (i > 0) {
                  Write(", ");
                }
                Translate(((CiCondExpr)iifs[i]).Cond);
              }
              WriteLine("])) of");
              OpenBlock(false);
              for (int i = 0; i < (1 << level); i++) {
                Write(i + ": ");
                BuildCond(i, expr, level);
                WriteLine(";");
              }
              CloseBlock(false);
              Write("end");
              processed = true;
            }
            else {
              Write("if ");
              Translate(((CiCondExpr)iifs[0]).Cond);
              Write(" then ");
              BuildCond(1, expr, 1);
              Write(" else ");
              BuildCond(0, expr, 1);
              processed = true;
            }
          }
        }
      }
      if (!processed) {
        WriteMethodCall2(expr, null);
      }
    }

    void WriteInline(CiType type, CiMaybeAssign expr) {
      if (expr is CiExpr) {
        var exp = (CiExpr)expr;
        WriteExpr(type ?? GetExprType(exp), exp);
      }
      else {
        Statement_CiAssign((CiAssign)expr);
      }
    }

    void WriteAssign(CiVar Target, CiExpr Source) {
      if (!InContext(1) && (Source is CiCondExpr)) {
        CiCondExpr expr = (CiCondExpr)Source;
        Write("if ");
        WriteChild(expr, expr.Cond, true);
        Write(" then ");
        WriteAssign(Target);
        WriteCondChild(expr, expr.OnTrue);
        Write(" else ");
        WriteAssign(Target);
        WriteCondChild(expr, expr.OnFalse);
      }
      else if (Source is CiNewExpr) {
        WriteAssignNew(Target, ((CiNewExpr)Source).NewType);
      }
      else {
        EnterContext(1);
        WriteAssign(Target);
        WriteInline(Target.Type, Source);
        ExitContext();
      }
			
    }

    void WriteAssign(CiExpr Target, CiToken Op, CiMaybeAssign Source) {
      if (Source is CiCondExpr) {
        CiCondExpr expr = (CiCondExpr)Source;
        Write("if ");
        WriteChild(expr, expr.Cond, true);
        Write(" then ");
        WriteAssign(Target, Op);
        WriteCondChild(expr, expr.OnTrue);
        Write(" else ");
        WriteAssign(Target, Op);
        WriteCondChild(expr, expr.OnFalse);
      }
      else if (Source is CiNewExpr) {
        WriteAssignNew(Target, Op, ((CiNewExpr)Source).NewType);
      }
      else {
        EnterContext(1);
        WriteAssign(Target, Op);
        WriteInline(Target.Type, Source);
        ExitContext();
      }
    }

    void WriteAssign(CiVar Target) {
      WriteFormat("{0}:= ", DecodeSymbol(Target));
    }

    void WriteAssign(CiExpr Target, CiToken Op) {
      Translate(Target);
      Write(":= ");
      if (Op != CiToken.Assign) {
        Translate(Target);

        switch (Op) {
          case CiToken.AddAssign:
            Write(" + ");
            break;
          case CiToken.SubAssign:
            Write(" - ");
            break;
          case CiToken.MulAssign:
            Write(" * ");
            break;
          case CiToken.DivAssign:
            Write(DecodeDivSymbol(GetExprType(Target)));
            break;
          case CiToken.ModAssign:
            Write(" mod ");
            break;
          case CiToken.ShiftLeftAssign:
            Write(" shl ");
            break;
          case CiToken.ShiftRightAssign:
            Write(" shr ");
            break;
          case CiToken.AndAssign:
            Write(" and ");
            break;
          case CiToken.OrAssign:
            Write(" or ");
            break;
          case CiToken.XorAssign:
            Write(" xor ");
            break;
          default:
            throw new ArgumentException(Op.ToString());
        }
      }
    }

    void WriteExpr(CiType type, CiExpr expr) {
      if (expr is CiConstExpr) {
        Write(DecodeValue(type, ((CiConstExpr)expr).Value));
      }
      else {
        Translate(expr);
      }
    }

    void WriteCondChild(CiCondExpr condExpr, CiExpr expr) {
      if (condExpr.ResultType == CiByteType.Value && expr is CiConstExpr) {
//        Write("byte(");
        WriteChild(condExpr, expr);
//        Write(")");
      }
      else {
        WriteChild(condExpr, expr);
      }
    }

    void WriteInline(CiMaybeAssign expr) {
      if (expr is CiExpr)
        Translate((CiExpr)expr);
      else
        Statement_CiAssign((CiAssign)expr);
    }

    #region BreakTracker
    private Dictionary<ICiStatement, BeakInfo> mapping = new  Dictionary<ICiStatement, BeakInfo>();
    private Dictionary<CiMethod, List<BeakInfo>> methods = new Dictionary<CiMethod, List<BeakInfo>>();
    private Stack<BeakInfo> exitPoints = new Stack<BeakInfo>();
    //
    public void AddSwitch(CiMethod method, CiSwitch aSymbol) {
      List<BeakInfo> labels = null;
      methods.TryGetValue(method, out labels);
      if (labels == null) {
        labels = new List<BeakInfo>();
        methods.Add(method, labels);
      }
      BeakInfo label = new BeakInfo("goto__" + labels.Count);
      labels.Add(label);
      mapping.Add(aSymbol, label);
    }

    public List<BeakInfo> GetExitLabels(CiMethod method) {
      List<BeakInfo> labels = null;
      methods.TryGetValue(method, out labels);
      return labels;
    }

    public BeakInfo GetExitLabel(ICiStatement stmt) {
      BeakInfo label = null;
      mapping.TryGetValue(stmt, out label);
      return label;
    }

    public void ResetSwitch() {
      mapping.Clear();
      methods.Clear();
    }

    public BeakInfo EnterBreakableBlock(ICiStatement stmt) {
      BeakInfo label = GetExitLabel(stmt);
      exitPoints.Push(label);
      return label;
    }

    public void ExitBreakableBlock() {
      exitPoints.Pop();
    }

    public BeakInfo CurrentBreakableBlock() {
      if (exitPoints.Count == 0) {
        return null;
      }
      return exitPoints.Peek();
    }
    #endregion

  }
}
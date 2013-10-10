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

  public class GenPas : DelegateGenerator {
 
    public GenPas(string aNamespace) : this() {
      SetNamespace(aNamespace);
    }

    public GenPas() : base() {
      Namespace = "cito";
      BlockCloseStr = "end";
      BlockOpenStr = "begin";
      BlockCloseCR = false;
    }
    #region Base Generator specialization
    public override void Write(CiProgram prog) {
      BreakExit.Reset();
      NoIIFExpand.Reset();
      ExprType.Reset();
      MethodStack.Reset();
      base.Write(prog);
    }

    protected override string[] GetReservedWords() {
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
        "on",
        "operator",
        "or",
        "out",
        "packed",
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

    protected string DecodeType(CiType type) {
      return TypeMapper.GetTypeInfo(type).Name;
    }

    protected override string DecodeValue(CiType type, object value) {
      StringBuilder res = new StringBuilder();
      if (value is string) {
        res.Append('\'');
        foreach (char c in (string) value) {
          if ((int)c < 32) {
            res.Append("'+chr(" + (int)c + ")+'");
          }
          else if (c == '\'') {
            res.Append("''");
          }
          else {
            res.Append(c);
          }
        }
        res.Append('\'');
      }
      else if (value is Array) {
        res.Append("( ");
        res.Append(DecodeArray(type, (Array)value));
        res.Append(" )");
      }
      else if (value == null) {
        TypeMappingInfo info = TypeMapper.GetTypeInfo(type);
        res.Append(info.Null);
      }
      else if (value is bool) {
        res.Append((bool)value ? "true" : "false");
      }
      else if (value is byte) {
        res.Append((byte)value);
      }
      else if (value is int) {
        res.Append((int)value);
      }
      else if (value is CiEnumValue) {
        CiEnumValue ev = (CiEnumValue)value;
        res.Append(ev.Type.Name);
        res.Append('.');
        res.Append(ev.Name);
      }
      else {
        throw new ArgumentException(value.ToString());
      }
      return res.ToString();
    }

    protected override CiPriority GetPriority(CiExpr expr) {
      if (expr is CiPropertyAccess) {
        CiProperty prop = ((CiPropertyAccess)expr).Property;
        if (prop == CiLibrary.SByteProperty || prop == CiLibrary.LowByteProperty)
          return CiPriority.Prefix;
      }
      else if (expr is CiCoercion) {
        CiCoercion c = (CiCoercion)expr;
        if (c.ResultType == CiByteType.Value && c.Inner.Type == CiIntType.Value)
          return CiPriority.Prefix;
      }
      return base.GetPriority(expr);
    }

    protected StringBuilder oldLine = null;
    private static char[] trimmedChar = new char[] { '\r', '\t', '\n', ' ' };
    private static string trimmedString = new string(trimmedChar);

    protected override void WriteLine() {
      string newTxt = curLine.ToString().Trim(trimmedChar);
      if (curLine.Equals("")) {
        oldLine.Append(NewLineStr);
        return;
      }
      string oldTxt = oldLine.ToString();
      if (newTxt.StartsWith("else")) {
        for (int i=oldTxt.Length-1; i>=0; i--) {
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
        for (int i=oldTxt.Length-1; i>=0; i--) {
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
      fullCode.Append(oldTxt);
      oldLine = new StringBuilder();
      oldLine.Append(GetIndentStr());
      oldLine.Append(newTxt);
      oldLine.Append(NewLineStr);
      curLine = new StringBuilder();
    }

    protected override void Open(TextWriter writer) {
      oldLine = new StringBuilder();
      base.Open(writer);
    }

    protected override void Close() {
      if (oldLine.Length > 0) {
        fullCode.Append(oldLine);
      }
      base.Close();
    }

    protected virtual bool CheckCode(ICiStatement[] code) {
      CiBreak brk = (code != null) ? code[code.Length - 1] as CiBreak : null;
      return Execute(code, s => ((s is CiBreak) && (s != brk)));
    }

    protected override bool PreProcess(CiMethod method, ICiStatement stmt) {
      if (stmt is CiVar) {
        CiVar v = (CiVar)stmt;
        SymbolMapper parent = SymbolMapper.Find(method);
        string vName = SymbolMapper.GetPascalName(v.Name);
        // Look if local Ci var in already defined in Pascal procedure vars
        foreach (SymbolMapper item in parent.childs) {
          if (String.Compare(item.NewName, vName, true) == 0) {
            return false;
          }
        }
        SymbolMapper.AddSymbol(parent, v);
        TypeMapper.AddType(v.Type);
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
          BreakExit.AddSwitch(method, swith);
        }
      }
      return false;
    }
    #endregion
    #region Converter - Operator(x,y)
    protected override void InitOperators() {
      BinaryOperators.Add(CiToken.Plus, CiPriority.Additive, ConvertOperatorAssociative, " + ");
      BinaryOperators.Add(CiToken.Minus, CiPriority.Additive, ConvertOperatorNotAssociative, " - ");
      BinaryOperators.Add(CiToken.Asterisk, CiPriority.Multiplicative, ConvertOperatorAssociative, " * ");
      BinaryOperators.Add(CiToken.Slash, CiPriority.Multiplicative, ConvertOperatorSlash, null);
      BinaryOperators.Add(CiToken.Mod, CiPriority.Multiplicative, ConvertOperatorNotAssociative, " mod ");
      BinaryOperators.Add(CiToken.Less, CiPriority.Ordering, ConvertOperatorAssociative, " < ");
      BinaryOperators.Add(CiToken.LessOrEqual, CiPriority.Ordering, ConvertOperatorAssociative, " <= ");
      BinaryOperators.Add(CiToken.Greater, CiPriority.Ordering, ConvertOperatorNotAssociative, " > ");
      BinaryOperators.Add(CiToken.GreaterOrEqual, CiPriority.Ordering, ConvertOperatorAssociative, " >= ");
      BinaryOperators.Add(CiToken.Equal, CiPriority.Equality, ConvertOperatorAssociative, " = ");
      BinaryOperators.Add(CiToken.NotEqual, CiPriority.Equality, ConvertOperatorAssociative, " <> ");
      BinaryOperators.Add(CiToken.And, CiPriority.And, ConvertOperatorAssociative, " and ");
      BinaryOperators.Add(CiToken.Or, CiPriority.Or, ConvertOperatorAssociative, " or ");
      BinaryOperators.Add(CiToken.Xor, CiPriority.Xor, ConvertOperatorAssociative, " xor ");
      BinaryOperators.Add(CiToken.CondAnd, CiPriority.CondAnd, ConvertOperatorAssociative, " and ");
      BinaryOperators.Add(CiToken.CondOr, CiPriority.CondOr, ConvertOperatorAssociative, " or ");
      BinaryOperators.Add(CiToken.ShiftLeft, CiPriority.Shift, ConvertOperatorNotAssociative, " shl ");
      BinaryOperators.Add(CiToken.ShiftRight, CiPriority.Shift, ConvertOperatorNotAssociative, " shr ");
//
      UnaryOperators.Add(CiToken.Increment, CiPriority.Prefix, ConvertOperatorUnary, "__CINC_Pre(", ")");
      UnaryOperators.Add(CiToken.Decrement, CiPriority.Prefix, ConvertOperatorUnary, "__CINC_Pre(", ")");
      UnaryOperators.Add(CiToken.Minus, CiPriority.Prefix, ConvertOperatorUnary, "-(", ")");
      UnaryOperators.Add(CiToken.Not, CiPriority.Prefix, ConvertOperatorUnary, "not (", ")");
    }

    public void ConvertOperatorAssociative(CiBinaryExpr expr, BinaryOperatorInfo token) {
      // Work-around to have correct left and right type
      ExprType.Get(expr);
      if (token.ForcePar) {
        Write("(");
      }
      WriteChild(expr, expr.Left);
      Write(token.Symbol);
      WriteChild(expr, expr.Right);
      if (token.ForcePar) {
        Write(")");
      }
    }

    public void ConvertOperatorNotAssociative(CiBinaryExpr expr, BinaryOperatorInfo token) {
      // Work-around to have correct left and right type
      ExprType.Get(expr);
      if (token.ForcePar) {
        Write("(");
      }
      WriteChild(expr, expr.Left);
      Write(token.Symbol);
      WriteChild(expr, expr.Right, true);
      if (token.ForcePar) {
        Write(")");
      }
    }

    public void ConvertOperatorSlash(CiBinaryExpr expr, BinaryOperatorInfo token) {
      // Work-around to have correct left and right type
      ExprType.Get(expr);
      if (token.ForcePar) {
        Write("(");
      }
      WriteChild(expr, expr.Left);
      CiType type = ExprType.Get(expr.Left);
      Write(DecodeDivSymbol(type));
      WriteChild(expr, expr.Right, true);
      if (token.ForcePar) {
        Write(")");
      }
    }

    public void ConvertOperatorUnary(CiUnaryExpr expr, UnaryOperatorInfo token) {
      Write(token.Prefix);
      WriteChild(expr, expr.Inner);
      Write(token.Suffix);
    }
    #endregion
    #region Converter - Expression
    protected override void InitExpressions() {
      Expressions.Add(typeof(CiConstExpr), CiPriority.Postfix, Convert_CiConstExpr);
      Expressions.Add(typeof(CiConstAccess), CiPriority.Postfix, Convert_CiConstAccess);
      Expressions.Add(typeof(CiVarAccess), CiPriority.Postfix, Convert_CiVarAccess);
      Expressions.Add(typeof(CiFieldAccess), CiPriority.Postfix, Convert_CiFieldAccess);
      Expressions.Add(typeof(CiPropertyAccess), CiPriority.Postfix, Convert_CiPropertyAccess);
      Expressions.Add(typeof(CiArrayAccess), CiPriority.Postfix, Convert_CiArrayAccess);
      Expressions.Add(typeof(CiMethodCall), CiPriority.Postfix, Convert_CiMethodCall);
      Expressions.Add(typeof(CiBinaryResourceExpr), CiPriority.Postfix, Convert_CiBinaryResourceExpr);
      Expressions.Add(typeof(CiNewExpr), CiPriority.Postfix, Convert_CiNewExpr);
      Expressions.Add(typeof(CiUnaryExpr), CiPriority.Prefix, Convert_CiUnaryExpr);
      Expressions.Add(typeof(CiCondNotExpr), CiPriority.Prefix, Convert_CiCondNotExpr);
      Expressions.Add(typeof(CiPostfixExpr), CiPriority.Prefix, Convert_CiPostfixExpr);
      Expressions.Add(typeof(CiCondExpr), CiPriority.CondExpr, Convert_CiCondExpr);
      Expressions.Add(typeof(CiBinaryExpr), CiPriority.Highest, Convert_CiBinaryExpr);
      Expressions.Add(typeof(CiCoercion), CiPriority.Highest, Convert_CiCoercion);
    }

    public void Convert_CiConstExpr(CiExpr expression) {
      CiConstExpr expr = (CiConstExpr)expression;
      Write(DecodeValue(ExprType.Get(expr), expr.Value));
    }

    public void Convert_CiConstAccess(CiExpr expression) {
      CiConstAccess expr = (CiConstAccess)expression;
      Write(DecodeSymbol(expr.Const));
    }

    public void Convert_CiVarAccess(CiExpr expression) {
      CiVarAccess expr = (CiVarAccess)expression;
      string name = expr.Var.Name;
      if (name.Equals("this")) {
        Write("self");
      }
      else {
        Write(DecodeSymbol(expr.Var));
      }
    }

    public void Convert_CiFieldAccess(CiExpr expression) {
      CiFieldAccess expr = (CiFieldAccess)expression;
      WriteChild(expr, expr.Obj);
      Write('.');
      Write(DecodeSymbol(expr.Field));
    }

    public void Convert_CiPropertyAccess(CiExpr expression) {
      CiPropertyAccess expr = (CiPropertyAccess)expression;
      if (!Translate(expr)) {
        throw new ArgumentException(expr.Property.Name);
      }
    }

    public void Convert_CiArrayAccess(CiExpr expression) {
      CiArrayAccess expr = (CiArrayAccess)expression;
      WriteChild(expr, expr.Array);
      Write('[');
      Translate(expr.Index);
      Write(']');
    }

    public void Convert_CiMethodCall(CiExpr expression) {
      CiMethodCall expr = (CiMethodCall)expression;
      if (!Translate(expr)) {
        WriteMethodCall(expr);
      }
    }

    public void Convert_CiBinaryResourceExpr(CiExpr expression) {
      CiBinaryResourceExpr expr = (CiBinaryResourceExpr)expression;
      Write(DecodeSymbol(expr.Resource));
    }

    public void Convert_CiNewExpr(CiExpr expression) {
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

    public void Convert_CiUnaryExpr(CiExpr expression) {
      CiUnaryExpr expr = (CiUnaryExpr)expression;
      UnaryOperatorInfo tokenInfo = UnaryOperators.GetUnaryOperator(expr.Op);
      tokenInfo.WriteDelegate(expr, tokenInfo);
    }

    public void Convert_CiCondNotExpr(CiExpr expression) {
      CiCondNotExpr expr = (CiCondNotExpr)expression;
      Write("not (");
      WriteChild(expr, expr.Inner);
      Write(")");
    }

    public void Convert_CiPostfixExpr(CiExpr expression) {
      CiPostfixExpr expr = (CiPostfixExpr)expression;
      switch (expr.Op) {
        case CiToken.Increment:
          Write("__CINC_Post(");
          break;
        case CiToken.Decrement:
          Write("__CDEC_Post(");
          break;
        default:
          throw new ArgumentException(expr.Op.ToString());
      }
      WriteChild(expr, expr.Inner);
      Write(")");
    }

    public void Convert_CiCondExpr(CiExpr expression) {
      CiCondExpr expr = (CiCondExpr)expression;
      Write("IfThen(");
      WriteChild(expr, expr.Cond, true);
      Write(", ");
      WriteCondChild(expr, expr.OnTrue);
      Write(", ");
      WriteCondChild(expr, expr.OnFalse);
      Write(")");
    }

    public void Convert_CiBinaryExpr(CiExpr expression) {
      CiBinaryExpr expr = (CiBinaryExpr)expression;
      BinaryOperatorInfo tokenInfo = BinaryOperators.GetBinaryOperator(expr.Op);
      tokenInfo.WriteDelegate(expr, tokenInfo);
    }

    public void Convert_CiCoercion(CiExpr expression) {
      CiCoercion expr = (CiCoercion)expression;
      if (expr.ResultType == CiByteType.Value && expr.Inner.Type == CiIntType.Value) {
        Write("byte(");
        WriteChild(expr, (CiExpr)expr.Inner); // TODO: Assign
        Write(")");
      }
      else {
        WriteInline(expr.Inner);
      }
    }
    #endregion
    #region Converter - Symbols
    protected override void InitSymbols() {
      AddSymbol(typeof(CiEnum), Convert_CiEnum);
      AddSymbol(typeof(CiConst), Convert_CiConst);
      AddSymbol(typeof(CiField), Convert_CiField);
      AddSymbol(typeof(CiMacro), IgnoreSymbol);
      AddSymbol(typeof(CiMethod), IgnoreSymbol);
      AddSymbol(typeof(CiClass), IgnoreSymbol);
      AddSymbol(typeof(CiDelegate), IgnoreSymbol);
    }

    public void IgnoreSymbol(CiSymbol symbol) {
    }

    public void Convert_CiEnum(CiSymbol symbol) {
      CiEnum enu = (CiEnum)symbol;
      WriteLine();
      WriteCodeDoc(enu.Documentation);
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
        WriteCodeDoc(value.Documentation);
        Write(DecodeSymbol(value));
      }
      WriteLine();
      CloseBlock(false);
      WriteLine(");");
    }

    public void Convert_CiConst(CiSymbol symbol) {
      CiConst konst = (CiConst)symbol;
      WriteCodeDoc(konst.Documentation);
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

    public void Convert_CiField(CiSymbol symbol) {
      WriteField((CiField)symbol, null, true);
    }
    #endregion
    #region Converter - Statements
    protected override void InitStatements() {
      AddStatement(typeof(CiBlock), Convert_CiBlock);
      AddStatement(typeof(CiConst), IgnoreStatement);
      AddStatement(typeof(CiVar), Convert_CiVar);
      AddStatement(typeof(CiExpr), Convert_CiExpr);
      AddStatement(typeof(CiAssign), Convert_CiAssign);
      AddStatement(typeof(CiDelete), Convert_CiDelete);
      AddStatement(typeof(CiBreak), Convert_CiBreak);
      AddStatement(typeof(CiContinue), Convert_CiContinue);
      AddStatement(typeof(CiDoWhile), Convert_CiDoWhile);
      AddStatement(typeof(CiFor), Convert_CiFor);
      AddStatement(typeof(CiIf), Convert_CiIf);
      AddStatement(typeof(CiNativeBlock), Convert_CiNativeBlock);
      AddStatement(typeof(CiReturn), Convert_CiReturn);
      AddStatement(typeof(CiSwitch), Convert_CiSwitch);
      AddStatement(typeof(CiThrow), Convert_CiThrow);
      AddStatement(typeof(CiWhile), Convert_CiWhile);
    }

    public void IgnoreStatement(ICiStatement statement) {
    }

    public void Convert_CiBlock(ICiStatement statement) {
      CiBlock block = (CiBlock)statement;
      OpenBlock();
      WriteCode(block.Statements);
      CloseBlock();
    }

    public void Convert_CiVar(ICiStatement statement) {
      CiVar vr = (CiVar)statement;
      WriteInitVal(vr);
    }

    public virtual void Convert_CiExpr(ICiStatement statement) {
      Translate((CiExpr)statement);
    }

    public void Convert_CiAssign(ICiStatement statement) {
      CiAssign assign = (CiAssign)statement;
      if (assign.Source is CiAssign) {
        CiAssign src = (CiAssign)assign.Source;
        Convert_CiAssign(src);
        WriteLine(";");
        WriteAssign(assign.Target, assign.Op, src.Target);
      }
      else {
        WriteAssign(assign.Target, assign.Op, assign.Source);
      }
    }

    public void Convert_CiDelete(ICiStatement statement) {
      CiDelete stmt = (CiDelete)statement;
      if (stmt.Expr is CiVarAccess) {
        CiVar var = ((CiVarAccess)stmt.Expr).Var;
        if (var.Type is CiClassStorageType) {
          WriteFormat("FreeAndNil({0})", DecodeSymbol(var));
        }
        if (var.Type is CiClassPtrType) {
          WriteFormat("FreeAndNil({0})", DecodeSymbol(var));
        }
        else if (var.Type is CiArrayStorageType) {
          TypeMappingInfo info = TypeMapper.GetTypeInfo(var.Type);
          WriteFormat("{0}:= {1}", DecodeSymbol(var), info.Null);
        }
        else if (var.Type is CiArrayPtrType) {
          TypeMappingInfo info = TypeMapper.GetTypeInfo(var.Type);
          WriteFormat("{0}:= {1}", DecodeSymbol(var), info.Null);

        }
      }
    }

    public void Convert_CiBreak(ICiStatement statement) {
      BreakExit label = BreakExit.Peek();
      if (label != null) {
        WriteLine("goto " + label.Name + ";");
      }
      else {
        WriteLine("break;");
      }
    }

    public virtual void Convert_CiContinue(ICiStatement statement) {
      WriteLine("continue;");
    }

    public void Convert_CiDoWhile(ICiStatement statement) {
      CiDoWhile stmt = (CiDoWhile)statement;
      BreakExit.Push(stmt);
      WriteLine("repeat");
      if ((stmt.Body != null) && (stmt.Body is CiBlock)) {
        OpenBlock(false);
        WriteCode(((CiBlock)stmt.Body).Statements);
        CloseBlock(false);
      }
      else {
        WriteChild(stmt.Body);
      }
      Write("until not(");
      Translate(stmt.Cond);
      WriteLine(");");
      BreakExit.Pop();
    }

    public void Convert_CiFor(ICiStatement statement) {
      CiFor stmt = (CiFor)statement;
      BreakExit.Push(stmt);
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
        if (stmt.Body is CiBlock) {
          WriteCode(((CiBlock)stmt.Body).Statements);
        }
        else {
          WriteStatement(stmt.Body);
        }
        Translate(stmt.Advance);
        WriteLine(";");
        CloseBlock();
      }
      else {
        WriteChild(stmt.Body);
      }
      BreakExit.Pop();
    }

    public void Convert_CiIf(ICiStatement statement) {
      CiIf stmt = (CiIf)statement;
      Write("if ");
      NoIIFExpand.Push(1);
      Translate(stmt.Cond);
      NoIIFExpand.Pop();
      Write(" then ");
      WriteChild(stmt.OnTrue);
      if (stmt.OnFalse != null) {
        Write("else ");
        if (stmt.OnFalse is CiIf) {
          Write(" ");
          WriteStatement(stmt.OnFalse);
        }
        else {
          WriteChild(stmt.OnFalse);
        }
      }
    }

    public virtual void Convert_CiNativeBlock(ICiStatement statement) {
      CiNativeBlock block = (CiNativeBlock)statement;
      Write(block.Content);
    }

    public void Convert_CiReturn(ICiStatement statement) {
      CiReturn stmt = (CiReturn)statement;
      if (stmt.Value == null) {
        Write("exit");
      }
      else {
        NoIIFExpand.Push(1);
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
            CiMethod call = MethodStack.Peek();
            CiType type = (call != null ? call.Signature.ReturnType : null);
            Write(DecodeValue(type, ((CiConstExpr)stmt.Value).Value));
          }
          else {
            Translate(stmt.Value);
          }
          WriteLine(";");
        }
        Write("exit");
        NoIIFExpand.Pop();
      }
    }

    public void Convert_CiSwitch(ICiStatement statement) {
      CiSwitch swich = (CiSwitch)statement;
      BreakExit label = BreakExit.Push(swich);
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
      BreakExit.Pop();
      if (label != null) {
        WriteLine(label.Name + ": ;");
      }
    }

    public void Convert_CiThrow(ICiStatement statement) {
      CiThrow stmt = (CiThrow)statement;
      Write("Raise Exception.Create(");
      Translate(stmt.Message);
      WriteLine(");");
    }

    public void Convert_CiWhile(ICiStatement statement) {
      CiWhile stmt = (CiWhile)statement;
      BreakExit.Push(stmt);
      Write("while (");
      Translate(stmt.Cond);
      Write(") do ");
      WriteChild(stmt.Body);
      BreakExit.Pop();
    }
    #endregion
    // Emit pascal program
    public override void EmitProgram(CiProgram prog) {
      CreateFile(this.OutputFile);
      // Prologue
      WriteLine("unit " + (!string.IsNullOrEmpty(this.Namespace) ? this.Namespace : "cito") + ";");
      // Declaration
      EmitInterfaceHeader();
      if (!SymbolMapper.IsEmpty()) {
        WriteLine();
        WriteLine("type");
        OpenBlock(false);
        EmitEnums(prog);
        EmitSuperTypes();
        EmitClassesInterface(prog);
        CloseBlock(false);
      }
      EmitConstants(prog);
      // Implementation
      EmitImplementationHeader();
      EmitClassesImplementation();
      //Epilogue
      EmitInitialization(prog);
      WriteLine("end.");
      CloseFile();
    }

    public void EmitInterfaceHeader() {
      WriteLine();
      WriteLine("interface");
      WriteLine();
      WriteLine("uses SysUtils, StrUtils, Classes, Math;");
    }

    public void EmitEnums(CiProgram prog) {
      foreach (CiSymbol symbol in prog.Globals) {
        if (symbol is CiEnum) {
          Convert_CiEnum(symbol);
        }
      }
    }

    public void EmitSuperTypes() {
      bool sep = false;
      foreach (CiClass klass in TypeMapper.GetClassList()) {
        if (!sep) {
          WriteLine();
          sep = true;
        }
        WriteLine("{0} = class;", DecodeSymbol(klass));
      }
      HashSet<string> types = new HashSet<string>();
      foreach (CiType t in TypeMapper.GetTypeList()) {
        TypeMappingInfo info = TypeMapper.GetTypeInfo(t);
        if (!info.Native) {
          if (sep) {
            sep = false;
            WriteLine();
          }
          if (!types.Contains(info.Name)) {
            types.Add(info.Name);
            WriteLine("{0} = {1};", info.Name, info.Definition);
          }
        }
      }
    }

    public void EmitClassesInterface(CiProgram prog) {
      var delegates = prog.Globals.Where(s => s is CiDelegate);
      if (delegates.Count() > 0) {
        WriteLine();
        foreach (CiDelegate del in delegates) {
          WriteCodeDoc(del.Documentation);
          WriteSignature(null, del, true);
          WriteLine(";");
        }
      }
      foreach (CiClass klass in ClassOrder.GetList()) {
        EmitClassInterface(klass);
      }
    }

    public void EmitClassInterface(CiClass klass) {
      WriteLine();
      WriteCodeDoc(klass.Documentation);
      string baseType = (klass.BaseClass != null) ? DecodeSymbol(klass.BaseClass) : "TInterfacedObject";
      WriteFormat("{0} = class({1})", DecodeSymbol(klass), baseType);
      OpenBlock(false);
      foreach (CiSymbol member in klass.Members) {
        if (!(member is CiMethod)) {
          Translate(member);
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

    public void EmitImplementationHeader() {
      WriteLine();
      WriteLine("implementation");
      WriteLine();
      bool getResProc = false;
      bool first = true;
      HashSet<string> types = new HashSet<string>();
      foreach (CiType t in TypeMapper.GetTypeList()) {
        TypeMappingInfo info = TypeMapper.GetTypeInfo(t);
        if (!info.Native) {
          if (first) {
            first = false;
          }
          if (!types.Contains(info.Name)) {
            types.Add(info.Name);
            WriteLine("var {0}: {1};", info.Null, info.Name);
            WriteLine("procedure __CCLEAR(var x: {0}); overload; var i: integer; begin for i:= low(x) to high(x) do x[i]:= {1}; end;", info.Name, info.ItemDefault);
            WriteLine("procedure __CFILL (var x: {0}; v: {1}); overload; var i: integer; begin for i:= low(x) to high(x) do x[i]:= v; end;", info.Name, info.ItemType);
            WriteLine("procedure __CCOPY (const source: {0}; sourceStart: integer; var dest: {0}; destStart: integer; len: integer); overload; var i: integer; begin for i:= 0 to len do dest[i+destStart]:= source[i+sourceStart]; end;", info.Name);
            if ((info.ItemType != null) && (info.ItemType.Equals("byte"))) {
              getResProc = true;
            }
          }
        }
      }
      if (getResProc) {
        WriteLine("function  __getBinaryResource(const aName: string): ArrayOf_byte; var myfile: TFileStream; begin myFile:= TFileStream.Create(aName, fmOpenRead); SetLength(Result, myFile.Size); try myFile.seek(0, soFromBeginning); myFile.ReadBuffer(Result, myFile.Size); finally myFile.free; end; end;");
        WriteLine("function  __TOSTR (const x: ArrayOf_byte): string; var i: integer; begin Result:= ''; for i:= low(x) to high(x) do Result:= Result + chr(x[i]); end;");
      }
      WriteLine("function  __CDEC_Pre (var x: integer): integer; overload; inline; begin dec(x); Result:= x; end;");
      WriteLine("function  __CDEC_Post(var x: integer): integer; overload; inline; begin Result:= x; dec(x); end;");
      WriteLine("function  __CINC_Pre (var x: integer): integer; overload; inline; begin inc(x); Result:= x; end;");
      WriteLine("function  __CINC_Post(var x: integer): integer; overload; inline; begin Result:= x; inc(x); end;");
      WriteLine("function  __CDEC_Pre (var x: byte): byte; overload; inline; begin dec(x); Result:= x; end;");
      WriteLine("function  __CDEC_Post(var x: byte): byte; overload; inline; begin Result:= x; dec(x); end;");
      WriteLine("function  __CINC_Pre (var x: byte): byte; overload; inline; begin inc(x); Result:= x; end;");
      WriteLine("function  __CINC_Post(var x: byte): byte; overload; inline; begin Result:= x; inc(x); end;");
      WriteLine("function  __getMagic(const cond: array of boolean): integer; var i: integer; var o: integer; begin Result:= 0; for i:= low(cond) to high(cond) do begin if (cond[i]) then o:= 1 else o:= 0; Result:= Result shl 1 + o; end; end;");
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
            WriteFormat("{0}: {1}", DecodeSymbol(konst), DecodeType(konst.Type));
            WriteLine(";");
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
      foreach (CiClass klass in ClassOrder.GetList()) {
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
      WriteLine();
      WriteLine("initialization");
      OpenBlock(false);
      HashSet<string> types = new HashSet<string>();
      foreach (CiType t in TypeMapper.GetTypeList()) {
        TypeMappingInfo info = TypeMapper.GetTypeInfo(t);
        if (info.NullInit != null) {
          if (!types.Contains(info.Name)) {
            types.Add(info.Name);
            Write(info.NullInit);
            WriteLine(";");
          }
        }
      }
      foreach (CiSymbol symbol in prog.Globals) {
        if (symbol is CiClass) {
          CiClass klass = (CiClass)symbol;
          foreach (CiConst konst in klass.ConstArrays) {
            WriteConstFull(konst);
          }
        }
      }
      foreach (CiSymbol symbol in prog.Globals) {
        if (symbol is CiClass) {
          CiClass klass = (CiClass)symbol;
          foreach (CiBinaryResource resource in klass.BinaryResources) {
            WriteLine("{0}:= __getBinaryResource('{1}');", DecodeSymbol(resource), resource.Name);
          }
        }
      }
      CloseBlock(false);
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

    string EscapeComment(string text) {
      StringBuilder res = new StringBuilder(text.Length);
      foreach (char c in text) {
        switch (c) {
          case '{':
            res.Append('[');
            break;
          case '}':
            res.Append(']');
            break;
          case '\r':
            break;
          case '\n':
            break;
          default:
            res.Append(c);
            break;
        }
      }
      return res.ToString();
    }

    bool IsAssignmentOf(ICiStatement stmt, CiVar v4r) {
      CiVar v = null;
      if (stmt is CiPostfixExpr) {
        if (((CiPostfixExpr)stmt).Inner is CiVarAccess) {
          v = (CiVar)((CiVarAccess)(((CiPostfixExpr)stmt).Inner)).Var;
        }
      }
      if (stmt is CiVarAccess) {
        v = (CiVar)((CiVarAccess)stmt).Var;
      }
      if (stmt is CiAssign) {
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
        if ((cond.Left is CiVarAccess) && ((cond.Right is CiConstExpr) || (cond.Right is CiVarAccess))) {
          if (((CiVarAccess)cond.Left).Var == loopVar) {
            // loop variabale cannot be changed inside the loop
            if (Execute(stmt.Body, s => IsAssignmentOf(s, loopVar))) {
              return false;
            }
            return true;
          }
        }
      }
      return false;
    }

    void WriteMethodIntf(CiMethod method) {
      WriteCodeDoc(method.Documentation);
      foreach (CiParam param in method.Signature.Params) {
        if (param.Documentation != null) {
          WriteLine("{{ @param '{0}' ", DecodeSymbol(param));
          WriteDocPara(param.Documentation.Summary);
          WriteLine("}");
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
      foreach (CiSymbol member in klass.Members) {
        if (member is CiConst) {
          var konst = (CiConst)member;
          if (konst.Type is CiArrayType) {
            WriteConstFull(konst);
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
          WriteStatement(klass.Constructor.Body);
        }
      }
      CloseBlock();
      WriteLine(";");
    }

    void WriteMethodImpl(CiMethod method) {
      MethodStack.Push(method);
      WriteLine();
      if (method.CallType == CiCallType.Static) {
        Write("class ");
      }
      WriteSignature(method.Class.Name, method.Signature, false);
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
        WriteStatement(method.Body);
      }
      CloseBlock();
      WriteLine(";");
      MethodStack.Pop();
    }

    void WriteLabels(CiMethod method) {
      List<BreakExit> labels = BreakExit.GetLabels(method);
      if (labels != null) {
        foreach (BreakExit label in labels) {
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
      SymbolMapper vars = SymbolMapper.Find(symb);
      if (vars != null) {
        foreach (SymbolMapper var in vars.childs) {
          if (var.Symbol == null) {
            continue;
          }
          if (var.Symbol is CiVar) {
            WriteVar((CiVar)var.Symbol, var.NewName, true);
          }
          if (var.Symbol is CiField) {
            WriteField((CiField)var.Symbol, var.NewName, true);
          }
        }
      }
    }

    void WriteVarsInit(CiSymbol symb) {
      SymbolMapper vars = SymbolMapper.Find(symb);
      if (vars != null) {
        foreach (SymbolMapper var in vars.childs) {
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

    void WriteCode(ICiStatement[] block) {
      foreach (ICiStatement stmt in block) {
        WriteStatement(stmt);
      }
    }

    void WriteChild(ICiStatement stmt) {
      if (stmt is CiBlock) {
        WriteStatement((CiBlock)stmt);
      }
      else {
        if ((stmt is CiReturn) && (((CiReturn)stmt).Value != null)) {
          OpenBlock();
          WriteStatement(stmt);
          CloseBlock();
        }
        else {
          WriteStatement(stmt);
        }
      }
    }

    void WriteField(CiField field, string NewName, bool docs) {
      if (docs) {
        WriteCodeDoc(field.Documentation);
      }
      WriteLine("{0} {1}: {2};", DecodeVisibility(field.Visibility), DecodeSymbol(field), DecodeType(field.Type));
    }

    void WriteVar(CiVar var, string NewName, bool docs) {
      if (docs) {
        WriteCodeDoc(var.Documentation);
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
      if (Type is CiClassStorageType) {
        CiClassStorageType classType = (CiClassStorageType)Type;
        WriteLine("{0}:= {1}.Create();", DecodeSymbol(symbol), DecodeSymbol(classType.Class));
      }
      else if (Type is CiArrayStorageType) {
        CiArrayStorageType arrayType = (CiArrayStorageType)Type;
        WriteFormat("SetLength({0}, ", DecodeSymbol(symbol));
        if (arrayType.LengthExpr != null) {
          Translate(arrayType.LengthExpr);
        }
        else {
          Write(arrayType.Length);
        }
        WriteLine(");");
      }
    }

    void WriteInitVal(CiVar vr) {
      if (vr.InitialValue != null) {
        if (vr.Type is CiArrayStorageType) {
          WriteFormat("__CFILL({0}, ", DecodeSymbol(vr));
          Translate(vr.InitialValue);
          Write(")");
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
          WriteStatement(breakFound);
          breakFound = null;
        }
        if (!(bodyStmt is CiBreak)) {
          WriteStatement(bodyStmt);
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
      for (int i=0; i<expr.Arguments.Length; i++) {
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
      for (int i=0; i<level; i++, mask *= 2) {
        leaf[i] = ((cond & mask) == mask);
      }
      WriteMethodCall2(expr, leaf);
    }

    void WriteMethodCall(CiMethodCall expr) {
      bool processed = false;
      if (expr.Arguments.Length > 0) {
        if (!NoIIFExpand.In(1)) {
          List<CiExpr> iifs = expr.Arguments.Where(arg => arg is CiCondExpr).ToList();
          int level = iifs.Count;
          if (level > 0) {
            if (level > 1) {
              Write("case (__getMagic([");
              for (int i=0; i<level; i++) {
                if (i > 0) {
                  Write(", ");
                }
                Translate(((CiCondExpr)iifs[i]).Cond);
              }
              WriteLine("])) of");
              OpenBlock(false);
              for (int i=0; i<(1<<level); i++) {
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
        WriteExpr(type ?? ExprType.Get(exp), exp);
      }
      else {
        Convert_CiAssign((CiAssign)expr);
      }
    }

    void WriteAssign(CiVar Target, CiExpr Source) {
      if (!NoIIFExpand.In(1) && (Source is CiCondExpr)) {
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
        NoIIFExpand.Push(1);
        WriteAssign(Target);
        WriteInline(Target.Type, Source);
        NoIIFExpand.Pop();
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
        NoIIFExpand.Push(1);
        WriteAssign(Target, Op);
        WriteInline(Target.Type, Source);
        NoIIFExpand.Pop();
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
            Write(DecodeDivSymbol(ExprType.Get(Target)));
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

    void WriteStatement(ICiStatement stmt) {
      if (stmt == null) {
        return;
      }
      Translate(stmt);
      if (curLine.Length > 0) {
        WriteLine(";");
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

    void WriteDocPara(CiDocPara para) {
      foreach (CiDocInline inline in para.Children) {
        CiDocText text = inline as CiDocText;
        if (text != null) {
          Write(EscapeComment(text.Text));
          continue;
        }
        CiDocCode code = inline as CiDocCode;
        if (code != null) {
          switch (code.Text) {
            case "true":
              Write("<see langword=\"true\" />");
              break;
            case "false":
              Write("<see langword=\"false\" />");
              break;
            case "null":
              Write("<see langword=\"null\" />");
              break;
            default:
              WriteFormat("<c>{0}</c>", EscapeComment(code.Text));
              break;
          }
          continue;
        }
        throw new ArgumentException(inline.GetType().Name);
      }
    }

    void WriteDocBlock(CiDocBlock block) {
      CiDocList list = block as CiDocList;
      if (list != null) {
        WriteLine();
        WriteLine("/// <list type=\"bullet\">");
        foreach (CiDocPara item in list.Items) {
          Write("/// <item>");
          WriteDocPara(item);
          WriteLine("</item>");
        }
        Write("/// </list>");
        WriteLine();
        Write("/// ");
        return;
      }
      WriteDocPara((CiDocPara)block);
    }

    void WriteCodeDoc(CiCodeDoc doc) {
      if (doc == null) {
        return;
      }
      Write("{ ");
      WriteDocPara(doc.Summary);
      WriteLine(" }");
      if (doc.Details.Length > 0) {
        Write("{ <remarks>");
        foreach (CiDocBlock block in doc.Details) {
          WriteDocBlock(block);
        }
        WriteLine("</remarks> }");
      }
    }

    void WriteCondChild(CiCondExpr condExpr, CiExpr expr) {
      if (condExpr.ResultType == CiByteType.Value && expr is CiConstExpr) {
        Write("byte(");
        WriteChild(condExpr, expr);
        Write(")");
      }
      else {
        WriteChild(condExpr, expr);
      }
    }

    void WriteInline(CiMaybeAssign expr) {
      if (expr is CiExpr)
        Translate((CiExpr)expr);
      else
        Convert_CiAssign((CiAssign)expr);
    }
    #region CiTo Library handlers
    protected override void InitLibrary() {
      // Properties
      AddProperty(CiLibrary.SByteProperty, LibPropertySByte);
      AddProperty(CiLibrary.LowByteProperty, LibPropertyLowByte);
      AddProperty(CiLibrary.StringLengthProperty, LibPropertyStringLength);
      // Methods
      AddMethod(CiLibrary.MulDivMethod, LibMethodMulDiv);
      AddMethod(CiLibrary.CharAtMethod, LibMethodCharAt);
      AddMethod(CiLibrary.SubstringMethod, LibMethodSubstring);
      AddMethod(CiLibrary.ArrayCopyToMethod, LibMethodArrayCopy);
      AddMethod(CiLibrary.ArrayToStringMethod, LibMethodArrayToString);
      AddMethod(CiLibrary.ArrayStorageClearMethod, LibMethodArrayStorageClear);
    }

    public void LibPropertySByte(CiPropertyAccess expr) {
      Write("shortint(");
      WriteChild(expr, expr.Obj);
      Write(")");
    }

    public void LibPropertyLowByte(CiPropertyAccess expr) {
      Write("byte(");
      WriteChild(expr, expr.Obj);
      Write(")");
    }

    public void LibPropertyStringLength(CiPropertyAccess expr) {
      Write("Length(");
      WriteChild(expr, expr.Obj);
      Write(")");
    }

    public void LibMethodMulDiv(CiMethodCall expr) {
      Write("(int64(");
      WriteChild(CiPriority.Prefix, expr.Obj);
      Write(") * int64(");
      WriteChild(CiPriority.Multiplicative, expr.Arguments[0]);
      Write(") div ");
      WriteChild(CiPriority.Multiplicative, expr.Arguments[1], true);
      Write(")");
    }

    public void LibMethodCharAt(CiMethodCall expr) {
      Write("ord(");
      Translate(expr.Obj);
      Write("[");
      Translate(expr.Arguments[0]);
      Write("+1])");
    }

    public void LibMethodSubstring(CiMethodCall expr) {
      Write("MidStr(");
      Translate(expr.Obj);
      Write(", ");
      Translate(expr.Arguments[0]);
      Write("+1, ");
      Translate(expr.Arguments[1]);
      Write(")");
    }

    public void LibMethodArrayCopy(CiMethodCall expr) {
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

    public void LibMethodArrayToString(CiMethodCall expr) {
      Write("__TOSTR(");
      Translate(expr.Obj);
      //        Write(expr.Arguments[0]);
      //        Write(expr.Arguments[1]);
      Write(")");
    }

    public void LibMethodArrayStorageClear(CiMethodCall expr) {
      Write("__CCLEAR(");
      Translate(expr.Obj);
      Write(")");
    }
    #endregion
  }
}
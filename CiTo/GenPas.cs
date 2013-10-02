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

  public class GenPas : BaseGenerator, ICiStatementVisitor, ICiSymbolVisitor {
    //
    PascalPreProcessing prePro = new PascalPreProcessing();

    public GenPas(string aNamespace) : this() {
      SetNamespace(aNamespace);
    }

    public GenPas() : base() {
      Namespace = "cito";
      BlockCloseStr = "end";
      BlockOpenStr = "begin";
      BlockCloseCR = false;
    }
    #region SourceCodeLowLevelWrite
    public StringBuilder oldLine = null;
    private static char[] trimmedChar = new char[] { '\r', '\t', '\n', ' ' };
    private static string trimmedString = new string(trimmedChar);

    public override void WriteLine() {
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
      else if ((this.Indent == 0) && (newTxt.StartsWith("end;"))) {
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
      for (int i = 0; i < this.Indent; i++) {
        oldLine.Append(IndentStr);
      }
      oldLine.Append(newTxt);
      oldLine.Append(NewLineStr);
      curLine = new StringBuilder();
    }

    public override void Open(TextWriter writer) {
      oldLine = new StringBuilder();
      base.Open(writer);
    }

    public override void Close() {
      if (oldLine.Length > 0) {
        fullCode.Append(oldLine);
      }
      base.Close();
    }
    #endregion
    protected override void InitTokens() {
      Tokens.Add(CiToken.Plus, " + ", 2, true, CiPriority.Additive);
      Tokens.Add(CiToken.Minus, " - ", 2, false, CiPriority.Additive);
      Tokens.Add(CiToken.Asterisk, " * ", 2, true, CiPriority.Multiplicative);
      Tokens.Add(CiToken.Slash, "__INVALID__", 2, false, CiPriority.Multiplicative);
      Tokens.Add(CiToken.Mod, " mod ", 2, false, CiPriority.Multiplicative);
      Tokens.Add(CiToken.Less, " < ", 2, true, CiPriority.Ordering);
      Tokens.Add(CiToken.LessOrEqual, " <= ", 2, true, CiPriority.Ordering);
      Tokens.Add(CiToken.Greater, " > ", 2, true, CiPriority.Ordering);
      Tokens.Add(CiToken.GreaterOrEqual, " >= ", 2, true, CiPriority.Ordering);
      Tokens.Add(CiToken.Equal, " = ", 2, true, CiPriority.Equality);
      Tokens.Add(CiToken.NotEqual, " <> ", 2, true, CiPriority.Equality);
      Tokens.Add(CiToken.And, " and ", 2, true, CiPriority.And);
      Tokens.Add(CiToken.Or, " or ", 2, true, CiPriority.Or);
      Tokens.Add(CiToken.Xor, " xor ", 2, true, CiPriority.Xor);
      Tokens.Add(CiToken.CondAnd, " and ", 2, true, CiPriority.CondAnd);
      Tokens.Add(CiToken.CondOr, " or ", 2, true, CiPriority.CondOr);
      Tokens.Add(CiToken.ShiftLeft, " shl ", 2, false, CiPriority.Shift);
      Tokens.Add(CiToken.ShiftRight, " shr ", 2, false, CiPriority.Shift);
    }

    protected override void InitExpressions() {
      Expressions.Add(typeof(CiConstExpr), CiPriority.Postfix, ConvertConstExpr);
      Expressions.Add(typeof(CiConstAccess), CiPriority.Postfix, ConvertConstAccess);
      Expressions.Add(typeof(CiVarAccess), CiPriority.Postfix, ConvertVarAccess);
      Expressions.Add(typeof(CiFieldAccess), CiPriority.Postfix, ConvertFieldAccess);
      Expressions.Add(typeof(CiPropertyAccess), CiPriority.Postfix, ConvertPropertyAccess);
      Expressions.Add(typeof(CiArrayAccess), CiPriority.Postfix, ConvertArrayAccess);
      Expressions.Add(typeof(CiMethodCall), CiPriority.Postfix, ConvertMethodCall);
      Expressions.Add(typeof(CiBinaryResourceExpr), CiPriority.Postfix, ConvertBinaryResourceExpr);
      Expressions.Add(typeof(CiNewExpr), CiPriority.Postfix, ConvertNewExpr);
      Expressions.Add(typeof(CiUnaryExpr), CiPriority.Prefix, ConvertUnaryExpr);
      Expressions.Add(typeof(CiCondNotExpr), CiPriority.Prefix, ConvertCondNotExpr);
      Expressions.Add(typeof(CiPostfixExpr), CiPriority.Prefix, ConvertPostfixExpr);
      Expressions.Add(typeof(CiCondExpr), CiPriority.CondExpr, ConvertCondExpr);
      Expressions.Add(typeof(CiBinaryExpr), CiPriority.Highest, ConvertBinaryExpr);
      Expressions.Add(typeof(CiCoercion), CiPriority.Highest, ConvertCoercion);
    }

    protected override void WriteBanner() {
      WriteLine("(* Generated automatically with \"cito\". Do not edit. *)");
    }

    public override void Write(CiProgram prog) {
      CreateFile(this.OutputFile);
      prePro.Parse(prog);
      // Prologue
      WriteLine("unit " + (!string.IsNullOrEmpty(this.Namespace) ? this.Namespace : "cito") + ";");
      // Declaration
      EmitInterfaceHeader();
      if (!SymbolMapping.IsEmpty()) {
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

    public void ConvertConstExpr(CiExpr expression) {
      CiConstExpr expr = (CiConstExpr)expression;
      WriteValue(ExprType.Get(expr), expr.Value);
    }

    public void ConvertConstAccess(CiExpr expression) {
      CiConstAccess expr = (CiConstAccess)expression;
      WriteSymbol(expr.Const);
    }

    public void ConvertVarAccess(CiExpr expression) {
      CiVarAccess expr = (CiVarAccess)expression;
      string name = expr.Var.Name;
      if (name.Equals("this")) {
        Write("self");
      }
      else {
        WriteSymbol(expr.Var);
      }
    }

    public void ConvertFieldAccess(CiExpr expression) {
      CiFieldAccess expr = (CiFieldAccess)expression;
      WriteChild(expr, expr.Obj);
      Write('.');
      WriteSymbol(expr.Field);
    }

    public void ConvertPropertyAccess(CiExpr expression) {
      CiPropertyAccess expr = (CiPropertyAccess)expression;
      if (!Library.Translate(expr)) {
        throw new ArgumentException(expr.Property.Name);
      }
    }

    public void ConvertArrayAccess(CiExpr expression) {
      CiArrayAccess expr = (CiArrayAccess)expression;
      WriteChild(expr, expr.Array);
      Write('[');
      WriteExpr(expr.Index);
      Write(']');
    }

    public void ConvertMethodCall(CiExpr expression) {
      CiMethodCall expr = (CiMethodCall)expression;
      if (!Library.Translate(expr)) {
        WriteMethodCall(expr);
      }
    }

    public void ConvertBinaryResourceExpr(CiExpr expression) {
      CiBinaryResourceExpr expr = (CiBinaryResourceExpr)expression;
      WriteSymbol(expr.Resource);
    }

    public void ConvertNewExpr(CiExpr expression) {
      CiNewExpr expr = (CiNewExpr)expression;
      WriteExpr(ExprType.Get(expr), expr);
    }

    public void ConvertUnaryExpr(CiExpr expression) {
      CiUnaryExpr expr = (CiUnaryExpr)expression;
      switch (expr.Op) {
        case CiToken.Increment:
          Write("__CINC_Pre(");
          break;
        case CiToken.Decrement:
          Write("__CDEC_Pre(");
          break;
        case CiToken.Minus:
          Write("-(");
          break;
        case CiToken.Not:
          Write("not (");
          break;
        default:
          throw new ArgumentException(expr.Op.ToString());
      }
      WriteChild(expr, expr.Inner);
      Write(")");
    }

    public void ConvertCondNotExpr(CiExpr expression) {
      CiCondNotExpr expr = (CiCondNotExpr)expression;
      Write("not (");
      WriteChild(expr, expr.Inner);
      Write(")");
    }

    public void ConvertPostfixExpr(CiExpr expression) {
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

    public void ConvertCondExpr(CiExpr expression) {
      CiCondExpr expr = (CiCondExpr)expression;
      Write("IfThen(");
      WriteChild(expr, expr.Cond, true);
      Write(", ");
      WriteCondChild(expr, expr.OnTrue);
      Write(", ");
      WriteCondChild(expr, expr.OnFalse);
      Write(")");
    }

    public void ConvertBinaryExpr(CiExpr expression) {
      CiBinaryExpr expr = (CiBinaryExpr)expression;
      Write("(");
      // Work-around to have correct left and right type
      ExprType.Get(expr);
      WriteChild(expr, expr.Left);
      TokenInfo tokenInfo = Tokens.GetTokenInfo(expr.Op, 2);
      WriteOp(ExprType.Get(expr.Left), tokenInfo);
      if (tokenInfo.IsAssociative) {
        WriteChild(expr, expr.Right);
      }
      else {
        WriteChild(expr, expr.Right, true);
      }
      Write(")");
    }

    public void ConvertCoercion(CiExpr expression) {
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

    public void EmitInitialization(CiProgram prog) {
      WriteLine();
      WriteLine("initialization");
      OpenBlock(false);
      HashSet<string> types = new HashSet<string>();
      foreach (CiType t in SuperType.GetTypeList()) {
        TypeMappingInfo info = SuperType.GetTypeInfo(t);
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
            WriteSymbol(resource);
            Write(":= __getBinaryResource('");
            Write(resource.Name);
            WriteLine("');");
          }
        }
      }
      CloseBlock(false);
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
            WriteSymbol(konst);
            Write(": ");
            WriteType(konst.Type);
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
            WriteSymbol(resource);
            WriteLine(": ArrayOf_byte;");
          }
        }
      }
      if (!first) {
        CloseBlock(false);
      }
    }

    public void EmitEnums(CiProgram prog) {
      foreach (CiSymbol symbol in prog.Globals) {
        if (symbol is CiEnum) {
          Visit((CiEnum)symbol);
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
      WriteSymbol(klass);
      Write(" = ");
      Write("class(");
      if (klass.BaseClass != null) {
        WriteSymbol(klass.BaseClass);
      }
      else {
        Write("TInterfacedObject");
      }
      WriteLine(")");
      OpenBlock(false);
      foreach (CiSymbol member in klass.Members) {
        if (!(member is CiMethod)) {
          member.Accept(this);
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

    public void EmitInterfaceHeader() {
      WriteLine();
      WriteLine("interface");
      WriteLine();
      WriteLine("uses SysUtils, StrUtils, Classes, Math;");
    }

    public void EmitSuperTypes() {
      bool sep = false;
      foreach (CiClass klass in SuperType.GetClassList()) {
        if (!sep) {
          WriteLine();
          sep = true;
        }
        WriteSymbol(klass);
        WriteLine(" = class;");
      }
      HashSet<string> types = new HashSet<string>();
      foreach (CiType t in SuperType.GetTypeList()) {
        TypeMappingInfo info = SuperType.GetTypeInfo(t);
        if (!info.Native) {
          if (sep) {
            sep = false;
            WriteLine();
          }
          if (!types.Contains(info.Name)) {
            types.Add(info.Name);
            Write(info.Name);
            Write(" = ");
            Write(info.Definition);
            WriteLine(";");
          }
        }
      }
    }

    public void EmitImplementationHeader() {
      WriteLine();
      WriteLine("implementation");
      WriteLine();
      bool getResProc = false;
      bool first = true;
      HashSet<string> types = new HashSet<string>();
      foreach (CiType t in SuperType.GetTypeList()) {
        TypeMappingInfo info = SuperType.GetTypeInfo(t);
        if (!info.Native) {
          if (first) {
            first = false;
          }
          if (!types.Contains(info.Name)) {
            types.Add(info.Name);
            Write("var ");
            Write(info.Null);
            Write(": ");
            Write(info.Name);
            WriteLine(";");
            WriteLine("procedure __CCLEAR(var x: " + info.Name + "); overload; var i: integer; begin for i:= low(x) to high(x) do x[i]:= " + info.ItemDefault + "; end;");
            WriteLine("procedure __CFILL (var x: " + info.Name + "; v: " + info.ItemType + "); overload; var i: integer; begin for i:= low(x) to high(x) do x[i]:= v; end;");
            WriteLine("procedure __CCOPY (const source: " + info.Name + "; sourceStart: integer; var dest: " + info.Name + "; destStart: integer; len: integer); overload; var i: integer; begin for i:= 0 to len do dest[i+destStart]:= source[i+sourceStart]; end;");
            if ((info.ItemType != null) && (info.ItemType.Equals("byte"))) {
              getResProc = true;
            }
          }
        }
      }
      if (getResProc) {
        WriteLine("function  __getBinaryResource(const aName: string): ArrayOf_byte; var myfile: TFileStream; begin myFile := TFileStream.Create(aName, fmOpenRead); SetLength(Result, myFile.Size); try myFile.seek(0, soFromBeginning); myFile.ReadBuffer(Result, myFile.Size); finally myFile.free; end; end;");
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

    public void WriteOp(CiType type, TokenInfo tokenInfo) {
      if (tokenInfo.Token == CiToken.Slash) {
        if ((type is CiIntType) || (type is CiByteType)) {
          Write(" div ");
        }
        else {
          Write(" / ");
        }
      }
      else {
        Write(tokenInfo.Symbol);
      }
    }

    protected virtual void WriteSymbol(CiSymbol var) {
      SymbolMapping symbol = SymbolMapping.Find(var);
      Write(symbol != null ? symbol.NewName : var.Name);
    }

    protected void WriteType(CiType type) {
      Write(SuperType.GetTypeInfo(type).Name);
    }

    void WriteMethodIntf(CiMethod method) {
      WriteCodeDoc(method.Documentation);
      foreach (CiParam param in method.Signature.Params) {
        if (param.Documentation != null) {
          Write("{ @param '");
          WriteSymbol(param);
          Write("' ");
          WriteDocPara(param.Documentation.Summary);
          WriteLine("}");
        }
      }
      WriteVisibility(method.Visibility);
      Write(" ");
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
      Write("constructor ");
      WriteSymbol(klass);
      WriteLine(".Create;");
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
        WriteSymbol(del);
        Write(" = ");
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
        WriteSymbol(del);
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
        WriteSymbol(param);
        Write(": ");
        WriteType(param.Type);
      }
      Write(')');
      if (del.ReturnType != CiType.Void) {
        Write(": ");
        WriteType(del.ReturnType);
      }
      if (typeDeclare) {
        if (prefix != null) {
          Write(" of object");
        }
      }
    }

    protected virtual void WriteVars(CiSymbol symb) {
      SymbolMapping vars = SymbolMapping.Find(symb);
      if (vars != null) {
        foreach (SymbolMapping var in vars.childs) {
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

    protected virtual void WriteVarsInit(CiSymbol symb) {
      SymbolMapping vars = SymbolMapping.Find(symb);
      if (vars != null) {
        foreach (SymbolMapping var in vars.childs) {
          if (var.Symbol == null) {
            continue;
          }
          if (var.Symbol is CiVar) {
            CiVar v = (CiVar)var.Symbol;
            WriteInitNew(v, v.Type);
          }
          if (var.Symbol is CiField) {
            CiField f = (CiField)var.Symbol;
            WriteInitNew(f, f.Type);
            WriteInitVal(f);
          }
        }
      }
    }

    protected virtual void WriteCode(ICiStatement[] block) {
      foreach (ICiStatement stmt in block) {
        WriteStatement(stmt);
      }
    }

    protected void WriteChild(ICiStatement stmt) {
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

    public bool IsAssignment(ICiStatement stmt, CiVar v4r) {
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
    public bool ValidPascalFor(CiFor stmt) {
      if (!(stmt.Init is CiVar))
        return false;
      // Single variable
      var loopVar = (CiVar)stmt.Init;
      if (loopVar.InitialValue is CiCondExpr)
        return false;
      // Step must be variable (de|in)cremented
      if ((stmt.Advance is CiPostfixExpr) && (stmt.Cond is CiBoolBinaryExpr)) {
        if (!IsAssignment(stmt.Advance, loopVar))
          return false;
        CiBoolBinaryExpr cond = (CiBoolBinaryExpr)stmt.Cond;
        // bounded by const or var
        if ((cond.Left is CiVarAccess) && ((cond.Right is CiConstExpr) || (cond.Right is CiVarAccess))) {
          if (((CiVarAccess)cond.Left).Var == loopVar) {
            // loop varibale cannot be changed inside the loop
            if (PascalPreProcessing.Execute(stmt.Body, s => IsAssignment(s, loopVar)))
              return false;
            return true;
          }
        }
      }
      return false;
    }

    void WriteVisibility(CiVisibility visibility) {
      switch (visibility) {
        case CiVisibility.Dead:
          Write("private");
          break;
        case CiVisibility.Private:
          Write("private");
          break;
        case CiVisibility.Internal:
          Write("private");
          break;
        case CiVisibility.Public:
          Write("public");
          break;
      }
    }

    protected virtual void WriteField(CiField field, string NewName, bool docs) {
      if (docs) {
        WriteCodeDoc(field.Documentation);
      }
      WriteVisibility(field.Visibility);
      Write(" ");
      WriteSymbol(field);
      Write(": ");
      WriteType(field.Type);
      WriteLine(";");
    }

    protected virtual void WriteVar(CiVar var, string NewName, bool docs) {
      if (docs) {
        WriteCodeDoc(var.Documentation);
      }
      Write("var ");
      WriteSymbol(var);
      Write(": ");
      WriteType(var.Type);
      WriteLine(";");
    }

    protected void WriteNew(CiType type) {
      throw new InvalidOperationException("Unsupported call to WriteNew()");
    }

    void WriteAssignNew(CiVar Target, CiType Type) {
      if (Type is CiClassStorageType) {
        CiClassStorageType classType = (CiClassStorageType)Type;
        WriteSymbol(Target);
        Write(":= ");
        WriteSymbol(classType.Class);
        WriteLine(".Create();");
      }
      else if (Type is CiArrayStorageType) {
        CiArrayStorageType arrayType = (CiArrayStorageType)Type;
        Write("SetLength(");
        WriteSymbol(Target);
        Write(", ");
        if (arrayType.LengthExpr != null) {
          WriteExpr(arrayType.LengthExpr);
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
        WriteExpr(Target);
        Write(":= ");
        WriteSymbol(classType.Class);
        WriteLine(".Create();");
      }
      else if (Type is CiArrayStorageType) {
        CiArrayStorageType arrayType = (CiArrayStorageType)Type;
        Write("SetLength(");
        WriteExpr(Target);
        Write(", ");
        if (arrayType.LengthExpr != null) {
          WriteExpr(arrayType.LengthExpr);
        }
        else {
          Write(arrayType.Length);
        }
        WriteLine(");");
      }
      else if (Type is CiArrayPtrType) {
        CiArrayStorageType arrayType = (CiArrayStorageType)Type;
        Write("SetLength(");
        WriteExpr(Target);
        Write(", ");
        if (arrayType.LengthExpr != null) {
          WriteExpr(arrayType.LengthExpr);
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
        WriteSymbol(symbol);
        Write(":= ");
        WriteSymbol(classType.Class);
        WriteLine(".Create();");
      }
      else if (Type is CiArrayStorageType) {
        CiArrayStorageType arrayType = (CiArrayStorageType)Type;
        Write("SetLength(");
        WriteSymbol(symbol);
        Write(", ");
        if (arrayType.LengthExpr != null) {
          WriteExpr(arrayType.LengthExpr);
        }
        else {
          Write(arrayType.Length);
        }
        WriteLine(");");
      }
    }

    void WriteInitVal(CiSymbol symbol) {
      if (symbol is CiVar) {
        CiVar var = (CiVar)symbol;
        if (var.InitialValue != null) {
          if (var.Type is CiArrayStorageType) {
            Write("__CFILL(");
            WriteSymbol(var);
            Write(",");
            WriteExpr(var.InitialValue);
            Write(")");
          }
          else {
            WriteAssign(var, var.InitialValue);
          }
        }
      }
    }

    protected override void WriteValue(CiType type, object value) {
      if (value is string) {
        Write('\'');
        foreach (char c in (string) value) {
          if ((int)c < 32) {
            Write("'+chr(" + (int)c + ")+'");
          }
          else if (c == '\'') {
            Write("''");
          }
          else {
            Write(c);
          }
        }
        Write('\'');
      }
      else if (value is Array) {
        Write("( ");
        WriteArray(type, (Array)value);
        Write(" )");
      }
      else if (value == null) {
        TypeMappingInfo info = SuperType.GetTypeInfo(type);
        Write(info.Null);
      }
      else if (value is bool) {
        Write((bool)value ? "true" : "false");
      }
      else if (value is byte) {
        Write((byte)value);
      }
      else if (value is int) {
        Write((int)value);
      }
      else if (value is CiEnumValue) {
        WriteEnumValue((CiEnumValue)value);
      }
      else {
        throw new ArgumentException(value.ToString());
      }
    }

    private void WriteEnumValue(CiEnumValue ev) {
      Write(ev.Type.Name);
      Write('.');
      Write(ev.Name);
    }

    virtual protected void WriteConstFull(CiConst konst) {
      object value = konst.Value;
      if (value is Array) {
        CiType elemType = null;
        if (konst.Type is CiArrayStorageType) {
          CiArrayStorageType type = (CiArrayStorageType)konst.Type;
          elemType = type.ElementType;
        }
        Array array = (Array)value;
        Write("SetLength(");
        WriteSymbol(konst);
        Write(", ");
        Write(array.Length);
        WriteLine(");");
        for (int i = 0; i < array.Length; i++) {
          WriteSymbol(konst);
          Write("[" + i + "]:= ");
          WriteValue(elemType, array.GetValue(i));
          WriteLine(";");
        }
      }
      else {
        WriteSymbol(konst);
        Write(":= ");
        WriteValue(konst.Type, value);
        WriteLine(";");
				
      }
    }

    protected bool WriteCaseInternal(CiSwitch swich, CiCase kase, ICiStatement[] body, string prefix) {
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
            throw new InvalidOperationException("Unsupported  Fallthrough");
          }
        }
      }
      CloseBlock();
      return true;
    }

    protected virtual void WriteArguments(CiMethodCall expr, bool[] conds) {
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

    protected virtual void WriteDelegateCall(CiExpr expr) {
      WriteExpr(expr);
    }

    protected virtual void WriteMethodCall2(CiMethodCall expr, bool[] cond) {
      if (expr.Method != null) {
        if (expr.Obj != null) {
          WriteExpr(expr.Obj);
        }
        else {
          WriteSymbol(expr.Method.Class);
        }
        Write('.');
        WriteSymbol(expr.Method);
      }
      else {
        WriteDelegateCall(expr.Obj);
      }
      WriteArguments(expr, cond);
    }

    protected virtual void BuildCond(int cond, CiMethodCall expr, int level) {
      int mask = 1;
      bool[] leaf = new bool[level];
      for (int i=0; i<level; i++, mask *= 2) {
        leaf[i] = ((cond & mask) == mask);
      }
      WriteMethodCall2(expr, leaf);
    }

    protected virtual void WriteMethodCall(CiMethodCall expr) {
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
                WriteExpr(((CiCondExpr)iifs[i]).Cond);
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
              WriteExpr(((CiCondExpr)iifs[0]).Cond);
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

    protected void WriteInline(CiType type, CiMaybeAssign expr) {
      if (expr is CiExpr) {
        var exp = (CiExpr)expr;
        WriteExpr(type ?? ExprType.Get(exp), exp);
      }
      else {
        Visit((CiAssign)expr);
      }
    }

    public void WriteAssign(CiVar Target, CiExpr Source) {
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

    public void WriteAssign(CiExpr Target, CiToken Op, CiMaybeAssign Source) {
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

    public void WriteAssign(CiVar Target) {
      WriteSymbol(Target);
      Write(":= ");
    }

    public void WriteAssign(CiExpr Target, CiToken Op) {
      WriteExpr(Target);
      Write(":= ");
      if (Op != CiToken.Assign) {
        WriteExpr(Target);

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
            TokenInfo tokenInfo = Tokens.GetTokenInfo(CiToken.Slash);
            WriteOp(ExprType.Get(Target), tokenInfo);
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

    protected void WriteStatement(ICiStatement stmt) {
      if (stmt == null) {
        return;
      }
      stmt.Accept(this);
      if (curLine.Length > 0) {
        WriteLine(";");
      }
    }

    protected virtual void WriteExpr(CiType type, CiExpr expr) {
      if (expr is CiConstExpr) {
        WriteValue(type, ((CiConstExpr)expr).Value);
      }
      else {
        base.WriteExpr(expr);
      }
    }

    void WriteDoc(string text) {
      foreach (char c in text) {
        switch (c) {
          case '{':
            Write("[");
            break;
          case '}':
            Write("]");
            break;
          case '\r':
            break;
          case '\n':
            break;
          default:
            Write(c);
            break;
        }
      }
    }

    void WriteDocPara(CiDocPara para) {
      foreach (CiDocInline inline in para.Children) {
        CiDocText text = inline as CiDocText;
        if (text != null) {
          WriteDoc(text.Text);
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
              Write("<c>");
              WriteDoc(code.Text);
              Write("</c>");
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

    protected void WriteCodeDoc(CiCodeDoc doc) {
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

    protected void WriteInline(CiMaybeAssign expr) {
      if (expr is CiExpr)
        WriteExpr((CiExpr)expr);
      else
        Visit((CiAssign)expr);
    }
    #region SymbolVisitor implementation
    public void Visit(CiEnum enu) {
      WriteLine();
      WriteCodeDoc(enu.Documentation);
      WriteSymbol(enu);
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
        WriteSymbol(value);
      }
      WriteLine();
      CloseBlock(false);
      WriteLine(");");
    }

    void ICiSymbolVisitor.Visit(CiConst konst) {
      WriteCodeDoc(konst.Documentation);
      Write("public ");
      if (!(konst.Type is CiArrayType)) {
        Write("const ");
      }
      WriteSymbol(konst);
      Write(": ");
      WriteType(konst.Type);
      if (!(konst.Type is CiArrayType)) {
        Write(" = ");
        WriteValue(konst.Type, konst.Value);
      }
      WriteLine(";");
    }

    public void Visit(CiField field) {
      WriteField(field, null, true);
    }

    public void Visit(CiMethod method) {
      throw new InvalidOperationException("Try to visit a method");
    }

    public void Visit(CiClass klass) {
      throw new InvalidOperationException("Try to visit a Class");
    }

    public void Visit(CiDelegate del) {
      throw new InvalidOperationException("Unsupported Visit(CiDelegate)");
    }
    #endregion
    #region StatementVisitor implementation
    public void Visit(CiBlock block) {
      OpenBlock();
      WriteCode(block.Statements);
      CloseBlock();
    }

    public virtual void Visit(CiConst stmt) {
    }

    public void Visit(CiVar var) {
      WriteInitVal(var);
    }

    public virtual void Visit(CiExpr expr) {
      WriteExpr(expr);
    }

    public void Visit(CiAssign assign) {
      if (assign.Source is CiAssign) {
        CiAssign prev = (CiAssign)assign.Source;
        Visit(prev);
        WriteLine(";");
        WriteAssign(assign.Target, assign.Op, prev.Target);
      }
      else {
        WriteAssign(assign.Target, assign.Op, assign.Source);
      }
    }

    public void Visit(CiDelete stmt) {
      if (stmt.Expr is CiVarAccess) {
        CiVar var = ((CiVarAccess)stmt.Expr).Var;
        if (var.Type is CiClassStorageType) {
          Write("FreeAndNil(");
          WriteSymbol(var);
          Write(")");
        }
        else if (var.Type is CiArrayStorageType) {
          TypeMappingInfo info = SuperType.GetTypeInfo(var.Type);
          WriteSymbol(var);
          Write(":= ");
          Write(info.Null);
        }
        else if (var.Type is CiArrayPtrType) {
          TypeMappingInfo info = SuperType.GetTypeInfo(var.Type);
          WriteSymbol(var);
          Write(":= ");
          Write(info.Null);
        }
      }
    }

    public void Visit(CiBreak stmt) {
      BreakExit label = BreakExit.Peek();
      if (label != null) {
        WriteLine("goto " + label.Name + ";");
      }
      else {
        WriteLine("break;");
      }
    }

    public virtual void Visit(CiContinue stmt) {
      WriteLine("continue;");
    }

    public void Visit(CiDoWhile stmt) {
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
      WriteExpr(stmt.Cond);
      WriteLine(");");
      BreakExit.Pop();
    }

    public void Visit(CiFor stmt) {
      BreakExit.Push(stmt);
      bool hasInit = (stmt.Init != null);
      bool hasNext = (stmt.Advance != null);
      bool hasCond = (stmt.Cond != null);
      if (hasInit && hasNext && hasCond) {
        if (ValidPascalFor(stmt)) {
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
              WriteExpr(cond.Right);
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
        stmt.Init.Accept(this);
        WriteLine(";");
      }
      Write("while (");
      if (hasCond) {
        WriteExpr(stmt.Cond);
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
        stmt.Advance.Accept(this);
        WriteLine(";");
        CloseBlock();
      }
      else {
        WriteChild(stmt.Body);
      }
      BreakExit.Pop();
    }

    public void Visit(CiIf stmt) {
      Write("if ");
      NoIIFExpand.Push(1);
      WriteExpr(stmt.Cond);
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

    public virtual void Visit(CiNativeBlock statement) {
      Write(statement.Content);
    }

    public void Visit(CiReturn stmt) {
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
            WriteValue((call != null ? call.Signature.ReturnType : null), ((CiConstExpr)stmt.Value).Value);
          }
          else {
            WriteExpr(stmt.Value);
          }
          WriteLine(";");
        }
        Write("exit");
        NoIIFExpand.Pop();
      }
    }

    public void Visit(CiSwitch swich) {
      BreakExit label = BreakExit.Push(swich);
      Write("case (");
      WriteExpr(swich.Value);
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
          WriteValue(null, value);
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

    public void Visit(CiThrow stmt) {
      Write("Raise Exception.Create(");
      WriteExpr(stmt.Message);
      WriteLine(");");
    }

    public void Visit(CiWhile stmt) {
      BreakExit.Push(stmt);
      Write("while (");
      WriteExpr(stmt.Cond);
      Write(") do ");
      WriteChild(stmt.Body);
      BreakExit.Pop();
    }
    #endregion
    #region CiTo Library handlers
    protected override void InitLibrary() {
      // Properties
      Library.AddProperty(CiLibrary.SByteProperty, LibPropertySByte);
      Library.AddProperty(CiLibrary.LowByteProperty, LibPropertyLowByte);
      Library.AddProperty(CiLibrary.StringLengthProperty, LibPropertyStringLength);
      // Methods
      Library.AddMethod(CiLibrary.MulDivMethod, LibMethodMulDiv);
      Library.AddMethod(CiLibrary.CharAtMethod, LibMethodCharAt);
      Library.AddMethod(CiLibrary.SubstringMethod, LibMethodSubstring);
      Library.AddMethod(CiLibrary.ArrayCopyToMethod, LibMethodArrayCopy);
      Library.AddMethod(CiLibrary.ArrayToStringMethod, LibMethodArrayToString);
      Library.AddMethod(CiLibrary.ArrayStorageClearMethod, LibMethodArrayStorageClear);
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
      WriteExpr(expr.Obj);
      Write("[");
      WriteExpr(expr.Arguments[0]);
      Write("+1])");
    }

    public void LibMethodSubstring(CiMethodCall expr) {
      Write("MidStr(");
      WriteExpr(expr.Obj);
      Write(", ");
      WriteExpr(expr.Arguments[0]);
      Write("+1, ");
      WriteExpr(expr.Arguments[1]);
      Write(")");
    }

    public void LibMethodArrayCopy(CiMethodCall expr) {
      Write("__CCOPY(");
      WriteExpr(expr.Obj);
      Write(", ");
      WriteExpr(expr.Arguments[0]);
      Write(", ");
      WriteExpr(expr.Arguments[1]);
      Write(", ");
      WriteExpr(expr.Arguments[2]);
      Write(", ");
      WriteExpr(expr.Arguments[3]);
      Write(')');
    }

    public void LibMethodArrayToString(CiMethodCall expr) {
      Write("__TOSTR(");
      WriteExpr(expr.Obj);
      //        Write(expr.Arguments[0]);
      //        Write(expr.Arguments[1]);
      Write(")");
    }

    public void LibMethodArrayStorageClear(CiMethodCall expr) {
      Write("__CCLEAR(");
      WriteExpr(expr.Obj);
      Write(")");
    }
    #endregion
  }
}
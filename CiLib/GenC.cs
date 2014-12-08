// GenC.cs - C code generator
//
// Copyright (C) 2011-2014  Piotr Fusik
// Copyright (C) 2013-2014  Enrico Croce
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
using System.Linq;
using System.Text;

namespace Foxoft.Ci {

  public class GenC : CiGenerator {
    public GenC(string aNamespace) : this() {
      SetNamespace(aNamespace);
    }

    public GenC() : base() {
      Namespace = "cito";
      TranslateSymbolName = C_SymbolNameTranslator;
      Decode_NULLVALUE = "NULL";
      Decode_ENUMFORMAT = "{0}_{1}";
      Decode_TRUEVALUE = "TRUE";
      Decode_FALSEVALUE = "FALSE";
      CommentContinueStr = "* ";
      CommentBeginStr = "/**";
      CommentEndStr = "*/";
      CommentCodeBegin = "<code>";
      CommentCodeEnd = "</code>";
      CommentListBegin = "<ul>";
      CommentListEnd = "</ul>";
      CommentItemListBegin = "<li>";
      CommentItemListEnd = "</li>";
      SimpleCommentFormat = "/*{0} */";
    }

    public string C_SymbolNameTranslator(CiSymbol aSymbol) {
      if (aSymbol == null) {
        return "";
      }
      String name = aSymbol.Name;
      if (aSymbol is CiEnumValue) {
        name = ToUppercaseWithUnderscores(name);
      }
      else if (aSymbol is CiField) {
        name = ToCamelCase(name);
      }
      else if (aSymbol is CiMethod) {
        name = ToCapitalize(name);
      }
      else if (aSymbol is CiDelegate) {
        name = ToCapitalize(name);
      }
      else if (aSymbol is CiConst) {
        CiConst konst = (CiConst)aSymbol;
        string prefix = "";
        if (konst.Class != null) {
          prefix = DecodeSymbol(konst.Class) + "_";
        }
        if (konst.Type is CiArrayType) {
          name = prefix + ToCapitalize(name);
        }
        else {
          name = prefix + ToUppercaseWithUnderscores(name);
        }
      }
      else if (aSymbol is CiBinaryResource) {
        StringBuilder res = new StringBuilder();
        res.Append("CiBinaryResource_");
        CiBinaryResource resource = (CiBinaryResource)aSymbol;
        foreach (char c in resource.Name) {
          res.Append(CiLexer.IsLetter(c) ? c : '_');
        }
        name = res.ToString();
      }
      else {
        name = SymbolNameTranslator(aSymbol);
      }
      return name;
    }

    public override TypeInfo Type_CiClassStorageType(CiType type) {
      TypeInfo result = new TypeInfo(type, type.Name, "NULL");
      return result;
    }

    public override TypeInfo Type_CiByteType(CiType type) {
      TypeInfo result = new TypeInfo(type, "unsigned char", "0");
      return result;
    }

    public override TypeInfo Type_CiStringPtrType(CiType type) {
      TypeInfo result = new TypeInfo(type, "const char *", "NULL");
      return result;
    }

    public override void EmitProgram(CiProgram prog) {
      string headerFile = Path.ChangeExtension(this.OutputFile, "h");
      CreateFile(headerFile);
      WriteGuard("#ifndef");
      WriteGuard("#define");
      WriteBoolType();
      WriteLine("#ifdef __cplusplus");
      WriteLine("extern \"C\" {");
      WriteLine("#endif");
      WriteTypedefs(prog, CiVisibility.Public);
      foreach (CiSymbol symbol in prog.Globals) {
        if (symbol is CiClass && symbol.Visibility == CiVisibility.Public) {
          WriteSignatures((CiClass)symbol, true);
        }
      }
      WriteLine();
      WriteLine("#ifdef __cplusplus");
      WriteLine("}");
      WriteLine("#endif");
      WriteLine("#endif");
      CloseFile();
      CreateFile(this.OutputFile);
      WriteLine("#include <stdlib.h>");
      WriteLine("#include <string.h>");
      Write("#include \"");
      Write(Path.GetFileName(headerFile));
      WriteLine("\"");
      WriteTypedefs(prog, CiVisibility.Internal);
      foreach (CiSymbol symbol in prog.Globals) {
        if (symbol is CiClass) {
          WriteStruct((CiClass)symbol);
        }
      }
      foreach (CiSymbol symbol in prog.Globals) {
        if (symbol is CiClass) {
          WriteCode((CiClass)symbol);
        }
      }
      CloseFile();
    }

    #region Converter Symbols
    public override void Symbol_CiEnum(CiSymbol symbol) {
      CiEnum enu = (CiEnum)symbol;
      WriteLine();
      WriteDocCode(enu.Documentation);
      Write("typedef enum ");
      OpenBlock();
      bool first = true;
      foreach (CiEnumValue value in enu.Values) {
        if (first) {
          first = false;
        }
        else {
          WriteLine(",");
        }
        WriteDocCode(value.Documentation);
        WriteFormat("{0}_{1}", DecodeSymbol(enu), DecodeSymbol(value));
      }
      WriteLine();
      CloseBlock();
      Write(DecodeSymbol(enu));
      WriteLine(";");
    }

    public override void Symbol_CiField(CiSymbol symbol) {
      CiField field = (CiField)symbol;
      WriteDocCode(field.Documentation);
      Write(ToString(field.Type, field));
      WriteLine(";");
    }

    public override void Symbol_CiConst(CiSymbol symbol) {
      CiConst konst = (CiConst)symbol;
      if (konst.Type is CiArrayType) {
        WriteLine("static const {0} = {1};", ToString(konst.Type, konst), DecodeValue(konst.Type, konst.Value));
      }
    }

    public override void Symbol_CiMethod(CiSymbol symbol) {
      CiMethod method = (CiMethod)symbol;
      if (method.Visibility == CiVisibility.Dead || method.CallType == CiCallType.Abstract) {
        return;
      }
      WriteLine();
      EnterMethod(method);
      WriteSignature(method);
      WriteLine();
      OpenBlock();
      CiBlock block = method.Body as CiBlock;
      if (block != null) {
        ICiStatement[] statements = block.Statements;
        StartBlock(statements);
        if (method.Throws && method.Signature.ReturnType == CiType.Void && method.Body.CompletesNormally) {
          if (!TryWriteCallAndReturn(statements, statements.Length - 1, null)) {
            WriteCode(statements);
            WriteReturnTrue();
          }
        }
        else {
          WriteCode(statements);
        }
      }
      else {
        Translate(method.Body);
      }
      CloseBlock();
      ExitMethod();
    }

    public override void Symbol_CiDelegate(CiSymbol symbol) {
      CiDelegate del = (CiDelegate)symbol;
      if (del.WriteStatus == CiWriteStatus.Done) {
        return;
      }
      if (del.WriteStatus == CiWriteStatus.InProgress) {
        throw new ResolveException(symbol, "Circular dependency for delegate {0}");
      }
      del.WriteStatus = CiWriteStatus.InProgress;
      foreach (CiParam param in del.Params) {
        CiDelegate paramDel = param.Type as CiDelegate;
        if (paramDel != null) {
          Translate(paramDel);
        }
      }
      del.WriteStatus = CiWriteStatus.Done;
      WriteLine();
      WriteDocCode(del.Documentation);
      WriteLine("typedef struct ");
      OpenBlock();
      WriteLine("void *obj;");
      string[] paramz = del.Params.Select(param => ", " + ToString(param.Type, param)).ToArray();
      string s = "(*func)(void *obj" + string.Concat(paramz) + ")";
      Write(ToString(del.ReturnType, s));
      WriteLine(";");
      CloseBlock();
      WriteLine("{0};", DecodeSymbol(del));
    }
    #endregion

    #region Converter Expression
    public override void Expression_CiVarAccess(CiExpr expression) {
      CiVarAccess expr = (CiVarAccess)expression;
      if (expr.Var == CurrentMethod().This) {
        Write("self");
      }
      else {
        base.Expression_CiVarAccess(expr);
      }
    }

    public override void Expression_CiFieldAccess(CiExpr expression) {
      CiFieldAccess expr = (CiFieldAccess)expression;
      StartFieldAccess(expr.Obj);
      Write(DecodeSymbol(expr.Field));
    }

    public override void Expression_CiMethodCall(CiExpr expression) {
      CiMethodCall call = (CiMethodCall)expression;
      if (HasThrows(call)) {
        WrapCall(call, null);
      }
      else {
        ConvertMethodCall(call);
      }
    }

    public void ConvertMethodCall(CiMethodCall expr) {
      if (!Translate(expr)) {
        bool first = true;
        if (expr.Method != null) {
          switch (expr.Method.CallType) {
            case CiCallType.Static:
              WriteFormat("{0}_{1}(", DecodeSymbol(expr.Method.Class), DecodeSymbol(expr.Method));
              break;
            case CiCallType.Normal:
              WriteFormat("{0}_{1}(", DecodeSymbol(expr.Method.Class), DecodeSymbol(expr.Method));
              Translate(expr.Obj);
              first = false;
              break;
            case CiCallType.Abstract:
            case CiCallType.Virtual:
            case CiCallType.Override:
              CiClass objClass = ((CiClassType)expr.Obj.Type).Class;
              CiClass ptrClass = GetVtblPtrClass(expr.Method.Class);
              CiClass defClass;
              for (defClass = expr.Method.Class; !AddsVirtualMethod(defClass, expr.Method.Name); defClass = defClass.BaseClass) {
              }
              if (defClass != ptrClass) {
                WriteFormat("((const {0}Vtbl *) ", DecodeSymbol(defClass));
              }
              StartFieldAccess(expr.Obj);
              for (CiClass baseClass = objClass; baseClass != ptrClass; baseClass = baseClass.BaseClass) {
                Write("base.");
              }
              Write("vtbl");
              if (defClass != ptrClass) {
                Write(')');
              }
              WriteFormat("->{0}(", DecodeSymbol(expr.Method));
              if (objClass == defClass) {
                Translate(expr.Obj);
              }
              else {
                Write('&');
                StartFieldAccess(expr.Obj);
                Write("base");
                for (CiClass baseClass = objClass.BaseClass; baseClass != defClass; baseClass = baseClass.BaseClass) {
                  Write(".base");
                }
              }
              first = false;
              break;
          }
        }
        else {
          // delegate
          Translate(expr.Obj);
          Write(".func(");
          Translate(expr.Obj);
          Write(".obj");
          first = false;
        }
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
        // if (expr.Method.Throws) Write(" /* throws */");
      }
    }

    protected void WrapCall(CiMethodCall call, CiAssign assign) {
      if (InContext(1)) {
        ConvertMethodCall(call);
        return;
      }
      EnterContext(1);
      object errorReturnValue = call.Method.ErrorReturnValue;
      Write("if (");
      if (false.Equals(errorReturnValue)) {
        Write('!');
        if (assign != null) {
          base.Statement_CiAssign(assign);
        }
        else {
          ConvertMethodCall(call);
        }
      }
      else {
        if (assign != null) {
          base.Statement_CiAssign(assign);
        }
        else {
          ConvertMethodCall(call);
        }
        WriteFormat(" == {0}", DecodeValue(null, errorReturnValue));
      }
      Write(")");
      if (!ForceBraceForSingleStatement) {
        WriteLine();
      }
      else {
        Write(" ");
      }
      OpenBlock(ForceBraceForSingleStatement);
      WriteLine("return {0};", DecodeValue(null, CurrentMethod().ErrorReturnValue));
      CloseBlock(ForceBraceForSingleStatement);
      ExitContext();
    }

    public override void Expression_CiCoercion(CiExpr expression) {
      CiCoercion expr = (CiCoercion)expression;
      if (expr.ResultType is CiClassType) {
        if (expr.ResultType is CiClassPtrType) {
          Write('&');
        }
        WriteChild(expr, (CiExpr)expr.Inner); // TODO: Assign
        CiClass klass = ((CiClassType)expr.Inner.Type).Class;
        if (expr.Inner.Type is CiClassPtrType) {
          Write("->base");
          klass = klass.BaseClass;
        }
        CiClass resultClass = ((CiClassType)expr.ResultType).Class;
        for (; klass != resultClass; klass = klass.BaseClass) {
          Write(".base");
        }
      }
      else {
        base.Expression_CiCoercion(expr);
      }
    }

    public override void InitOperators() {
      base.InitOperators();
      BinaryOperators.Declare(CiToken.Equal, CiPriority.Multiplicative, false, ConvertCString, " == ");
      BinaryOperators.Declare(CiToken.NotEqual, CiPriority.Multiplicative, false, ConvertCString, " != ");
      BinaryOperators.Declare(CiToken.Greater, CiPriority.Multiplicative, false, ConvertCString, " > ");
    }

    public void ConvertCString(CiBinaryExpr expr, BinaryOperatorInfo token) {
      if ((expr.Left.Type is CiStringType) && !expr.Left.IsConst(null) && !expr.Right.IsConst(null)) {
        Write("strcmp(");
        Translate(expr.Left);
        Write(", ");
        Translate(expr.Right);
        WriteFormat(") {0} 0", token.Symbol.Trim());
        return;
      }
      // optimize str.Length == 0, str.Length != 0, str.Length > 0
      CiPropertyAccess pa = expr.Left as CiPropertyAccess;
      if (pa != null && pa.Property == CiLibrary.StringLengthProperty) {
        CiConstExpr ce = expr.Right as CiConstExpr;
        if (ce != null && 0.Equals(ce.Value)) {
          WriteChild(CiPriority.Postfix, pa.Obj);
          Write(expr.Op == CiToken.Equal ? "[0] == '\\0'" : "[0] != '\\0'");
          return;
        }
      }
      ConvertOperator(expr, token);
    }
    #endregion

    #region Converter Statements
    public override void Statement_CiConst(ICiStatement statement) {
      CiConst konst = (CiConst)statement;
      Symbol_CiConst(konst);
    }

    public override void Statement_CiVar(ICiStatement statement) {
      CiVar stmt = (CiVar)statement;
      Write(ToString(stmt.Type, stmt));
      if (stmt.Type is CiClassStorageType) {
        CiClass klass = ((CiClassStorageType)stmt.Type).Class;
        if (klass.Constructs) {
          WriteLine(";");
          WriteConstruct(klass, stmt);
        }
      }
      else if (stmt.InitialValue != null) {
        if (stmt.Type is CiArrayStorageType) {
          WriteLine(";");
          WriteClearArray(new CiVarAccess { Var = stmt });
        }
        else if (IsInlineVar(stmt)) {
          Write(" = ");
          Translate(stmt.InitialValue);
        }
        else {
          WriteLine(";");
          Translate(new CiAssign { Target = new CiVarAccess { Var = stmt }, Op = CiToken.Assign, Source = stmt.InitialValue });
        }
      }
    }

    public override void Statement_CiAssign(ICiStatement statement) {
      CiAssign assign = (CiAssign)statement;
      if (assign.Target.Type is CiStringStorageType) {
        if (assign.Op == CiToken.Assign) {
          if (assign.Source is CiMethodCall) {
            CiMethodCall mc = (CiMethodCall)assign.Source;
            if (mc.Method == CiLibrary.SubstringMethod || mc.Method == CiLibrary.ArrayToStringMethod) {
              // TODO: make sure no side effects in mc.Arguments[1]
              Write("((char *) memcpy(");
              Translate(assign.Target);
              Write(", ");
              WriteSum(mc.Obj, mc.Arguments[0]);
              Write(", ");
              Translate(mc.Arguments[1]);
              Write("))[");
              Translate(mc.Arguments[1]);
              Write("] = '\\0'");
              return;
            }
          }
          if (assign.Source is CiConstExpr) {
            string s = ((CiConstExpr)assign.Source).Value as string;
            if (s != null && s.Length == 0) {
              Translate(assign.Target);
              Write("[0] = '\\0'");
              return;
            }
          }
          Write("strcpy(");
          Translate(assign.Target);
          Write(", ");
          // TODO: not an assignment
          Translate((CiExpr)assign.Source);
          Write(')');
          return;
        }
        if (assign.Op == CiToken.AddAssign) {
          Write("strcat(");
          Translate(assign.Target);
          Write(", ");
          // TODO: not an assignment
          Translate((CiExpr)assign.Source);
          Write(')');
          return;
        }
      }
      CiMethodCall call = assign.Source as CiMethodCall;
      if (HasThrows(call)) {
        WrapCall(call, assign);
      }
      else {
        base.Statement_CiAssign(assign);
      }
    }

    public override void Statement_CiDelete(ICiStatement statement) {
      CiDelete stmt = (CiDelete)statement;
      Write("free(");
      Translate(stmt.Expr);
      WriteLine(");");
    }

    public override void Statement_CiThrow(ICiStatement statement) {
      WriteLine("return {0};", DecodeValue(null, CurrentMethod().ErrorReturnValue));
    }

    public override void Statement_CiReturn(ICiStatement statement) {
      CiReturn stmt = (CiReturn)statement;
      if (false.Equals(CurrentMethod().ErrorReturnValue)) {
        WriteReturnTrue();
      }
      else {
        base.Statement_CiReturn(stmt);
      }
    }

    public override void Statement_CiIf(ICiStatement statement) {
      CiIf stmt = (CiIf)statement;
      Write("if (");
      Translate(stmt.Cond);
      Write(')');
      bool writeCond = true;
      if (stmt.OnFalse != null) {
        // avoid:
        // if (c)
        //    stmtThatThrows; // -> if (method() == ERROR_VALUE) return ERROR_VALUE;
        // else // mismatched if
        //    stmt;
        CiMethodCall call;
        CiAssign assign = stmt.OnTrue as CiAssign;
        if (assign != null) {
          call = assign.Source as CiMethodCall;
        }
        else {
          call = stmt.OnTrue as CiMethodCall;
        }
        if (HasThrows(call)) {
          Write(' ');
          OpenBlock();
          WriteChild(stmt.OnTrue);
          CloseBlock();
          writeCond = false;
        }
      }
      if (writeCond) {
        WriteChild(stmt.OnTrue);
      }
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
    #endregion

    void StartFieldAccess(CiExpr expr) {
      WriteChild(CiPriority.Postfix, expr);
      if (expr.Type is CiClassPtrType) {
        Write("->");
      }
      else {
        Write('.');
      }
    }

    static void InsertPtr(StringBuilder sb, PtrWritability wr) {
      sb.Insert(0, '*');
      if (wr != PtrWritability.ReadWrite) {
        sb.Insert(0, "const ");
      }
    }

    protected string ToString(CiType aType, CiSymbol aSymbol) {
      string name = DecodeSymbol(aSymbol);
      return ToString(aType, name);
    }

    protected string ToString(CiType aType, string name) {
      StringBuilder sb = new StringBuilder(name);
      bool needParens = false;
      while (aType is CiArrayType) {
        CiArrayStorageType stg = aType as CiArrayStorageType;
        if (stg != null) {
          if (needParens) {
            sb.Insert(0, '(');
            sb.Append(')');
            needParens = false;
          }
          sb.Append('[');
          sb.Append(stg.Length);
          sb.Append(']');
        }
        else {
          InsertPtr(sb, ((CiArrayPtrType)aType).Writability);
          needParens = true;
        }
        aType = ((CiArrayType)aType).ElementType;
      }
      if (aType is CiByteType) {
        sb.Insert(0, "unsigned char ");
      }
      else if (aType is CiStringPtrType) {
        sb.Insert(0, "const char *");
      }
      else if (aType is CiStringStorageType) {
        if (needParens) {
          sb.Insert(0, '(');
          sb.Append(')');
        }
        sb.Insert(0, "char ");
        sb.Append('[');
        sb.Append(((CiStringStorageType)aType).Length + 1);
        sb.Append(']');
      }
      else {
        if (aType is CiClassPtrType) {
          InsertPtr(sb, ((CiClassPtrType)aType).Writability);
        }
        sb.Insert(0, ' ');
        sb.Insert(0, DecodeType(aType));
      }
      return sb.ToString();
    }

    void Write(CiClass klass, CiConst konst) {
      WriteLine();
      WriteDocCode(konst.Documentation);
      if (konst.Type is CiArrayStorageType) {
        CiArrayStorageType stg = (CiArrayStorageType)konst.Type;
        WriteLine("extern const {0};", ToString(konst.Type, konst));
        WriteFormat("#define {0}_LENGTH {1}", DecodeSymbol(konst), stg.Length);
      }
      else {
        WriteFormat("#define {0} {1}", DecodeSymbol(konst), DecodeValue(konst.Type, konst.Value));
      }
      WriteLine();
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
        if (c.ResultType is CiClassType) {
          return c.ResultType is CiClassPtrType ? CiPriority.Prefix : CiPriority.Postfix;
        }
      }
      return base.GetPriority(expr);
    }

    protected void WriteClearArray(CiExpr expr) {
      Write("memset(");
      Translate(expr);
      Write(", 0, sizeof(");
      Translate(expr);
      Write("))");
    }

    void WriteChildWithSuggestedParentheses(CiBinaryExpr parent, CiExpr child, CiPriority suggestedParentPriority, bool assoc) {
      if (assoc && GetPriority(parent) == GetPriority(child)) {
        Translate(child);
      }
      else {
        WriteChild(suggestedParentPriority, child);
      }
    }

    public override void WriteNew(CiType type) {
      CiClassStorageType classType = type as CiClassStorageType;
      if (classType != null) {
        WriteFormat("{0}_New()", DecodeSymbol(classType.Class));
      }
      else {
        CiArrayStorageType arrayType = (CiArrayStorageType)type;
        Write('(');
        Write(ToString(arrayType.ElementType, "*"));
        Write(") malloc(");
        WriteChild(CiPriority.Multiplicative, arrayType.LengthExpr);
        Write(" * sizeof(");
        Write(ToString(arrayType.ElementType, string.Empty));
        Write("))");
      }
    }

    bool TryWriteCallAndReturn(ICiStatement[] statements, int lastCallIndex, CiExpr returnValue) {
      CiMethodCall call = statements[lastCallIndex] as CiMethodCall;
      if (!HasThrows(call)) {
        return false;
      }
      EnterContext(1);
      WriteCode(statements, lastCallIndex);
      Write("return ");
      Expression_CiMethodCall(call);
      object errorReturnValue = call.Method.ErrorReturnValue;
      if (!false.Equals(errorReturnValue)) {
        WriteFormat(" != {1}", DecodeValue(null, errorReturnValue));
      }
      if (returnValue != null) {
        Write(" ? ");
        Translate(returnValue);
        WriteFormat(" : {0}", DecodeValue(null, CurrentMethod().ErrorReturnValue));
      }
      WriteLine(";");
      ExitContext();
      return true;
    }

    protected override void WriteCode(ICiStatement[] statements) {
      int i = statements.Length - 2;
      if (i >= 0) {
        CiReturn ret = statements[i + 1] as CiReturn;
        if (ret != null && TryWriteCallAndReturn(statements, i, ret.Value)) {
          return;
        }
      }
      base.WriteCode(statements);
    }

    protected virtual void StartBlock(ICiStatement[] statements) {
    }

    void WriteChild(CiMaybeAssign expr) {
      if (expr is CiMethodCall) {
        Translate((CiMethodCall)expr);
      }
      else {
        Write('(');
        Translate((CiAssign)expr);
        Write(')');
      }
    }

    static bool HasThrows(CiExpr expr) {
      CiMethodCall call = expr as CiMethodCall;
      return call != null && call.Method != null && call.Method.Throws;
    }

    protected static bool IsInlineVar(CiVar def) {
      if (def.Type is CiClassStorageType) {
        CiClass klass = ((CiClassStorageType)def.Type).Class;
        return !klass.Constructs;
      }
      if (def.InitialValue == null) {
        return true;
      }
      if (def.Type is CiArrayStorageType) {
        return false;
      }
      if (def.Type is CiStringStorageType) {
        return def.InitialValue is CiConstExpr;
      }
      if (HasThrows(def.InitialValue)) {
        return false;
      }
      return true;
    }

    protected void WriteConstruct(CiClass klass, CiVar stmt) {
      WriteFormat("{0}_Construct(&{1}", DecodeSymbol(klass), DecodeSymbol(stmt));
      if (HasVirtualMethods(klass)) {
        Write(", NULL");
      }
      Write(')');
    }

    void WriteReturnTrue() {
      WriteLine("return {0};", DecodeValue(null, true));
    }

    protected override void StartCase(ICiStatement stmt) {
      // prevent "error: a label can only be part of a statement and a declaration is not a statement"
      if (stmt is CiVar) {
        WriteLine(";");
      }
    }

    void WriteSignature(CiMethod method) {
      if (method.Visibility != CiVisibility.Public) {
        Write("static ");
      }
      var paramz = method.Signature.Params.Select(param => ToString(param.Type, param));
      if (method.CallType != CiCallType.Static) {
        paramz = new string[1] { ToString(method.This.Type, "self") }.Concat(paramz);
      }
      string s = paramz.Any() ? string.Join(", ", paramz.ToArray()) : "void";
      s = String.Format("{0}_{1}({2})", DecodeSymbol(method.Class), DecodeSymbol(method), s);
      CiType type = method.Signature.ReturnType;
      if (method.Throws && type == CiType.Void) {
        type = CiBoolType.Value;
      }
      Write(ToString(type, s));
    }

    void WriteConstructorSignature(CiClass klass) {
      WriteFormat("static void {0}_Construct({1} *self", DecodeSymbol(klass), DecodeSymbol(klass));
      if (HasVirtualMethods(klass)) {
        WriteFormat(", const {0}Vtbl *vtbl", DecodeSymbol(GetVtblPtrClass(klass)));
      }
      Write(')');
    }

    void WriteNewSignature(CiClass klass) {
      WriteFormat("{0} *{1}_New(void)", DecodeSymbol(klass), DecodeSymbol(klass));
    }

    void WriteDeleteSignature(CiClass klass) {
      WriteFormat("void {0}_Delete({1} *self)", DecodeSymbol(klass), DecodeSymbol(klass));
    }

    static void ForEachStorageField(CiClass klass, Action<CiField, CiClass> action) {
      foreach (CiSymbol member in klass.Members) {
        CiField field = member as CiField;
        if (field != null) {
          CiClass storageClass = field.Type.StorageClass;
          if (storageClass != null) {
            action(field, storageClass);
          }
        }
      }
    }

    static bool HasCStruct(CiClass klass) {
      return klass.BaseClass != null || klass.HasFields || AddsVirtualMethods(klass);
    }

    void WriteNew(CiClass klass) {
      WriteNewSignature(klass);
      WriteLine();
      OpenBlock();
      WriteLine("{0} *self = ({0} *) malloc(sizeof({0}));", DecodeSymbol(klass));
      if (klass.Constructs) {
        WriteLine("if (self != NULL)");
        OpenBlock(false);
        WriteFormat("{0}_Construct(self", DecodeSymbol(klass));
        if (HasVirtualMethods(klass)) {
          Write(", NULL");
        }
        WriteLine(");");
        CloseBlock(false);
      }
      WriteLine("return self;");
      CloseBlock();
    }

    void WriteConstructorNewDelete(CiClass klass) {
      if (klass.Constructs) {
        WriteLine();
        EnterMethod(klass.Constructor);
        WriteConstructorSignature(klass);
        WriteLine();
        OpenBlock();
        if (klass.Constructor != null) {
          StartBlock(((CiBlock)klass.Constructor.Body).Statements);
        }
        CiClass ptrClass = GetVtblPtrClass(klass);
        if (HasVtblValue(klass)) {
          WriteLine("if (vtbl == NULL)");
          OpenBlock(false);
          Write("vtbl = ");
          CiClass structClass = GetVtblStructClass(klass);
          if (structClass != ptrClass) {
            WriteFormat("(const {0}Vtbl *) ", DecodeSymbol(ptrClass));
          }
          WriteLine("&CiVtbl_{0};", DecodeSymbol(klass));
          CloseBlock(false);
        }
        if (ptrClass == klass) {
          WriteLine("self->vtbl = vtbl;");
        }
        if (klass.BaseClass != null && klass.BaseClass.Constructs) {
          WriteFormat("{0}_Construct(&self->base", DecodeSymbol(klass.BaseClass));
          if (HasVirtualMethods(klass.BaseClass)) {
            Write(", vtbl");
          }
          WriteLine(");");
        }
        ForEachStorageField(klass, (field, fieldClass) => {
          if (fieldClass.Constructs) {
            WriteFormat("{0}_Construct(&self->{1}", DecodeSymbol(fieldClass), DecodeSymbol(field));
            if (HasVirtualMethods(fieldClass)) {
              Write(", NULL");
            }
            WriteLine(");");
          }
        });
        if (klass.Constructor != null) {
          WriteCode(((CiBlock)klass.Constructor.Body).Statements);
        }
        CloseBlock();
        ExitMethod();
      }
      if (!klass.IsAbstract && HasCStruct(klass)) {
        if (klass.Visibility == CiVisibility.Public) {
          WriteLine();
          WriteNew(klass);
          WriteLine();
          WriteDeleteSignature(klass);
          WriteLine();
          OpenBlock();
          WriteLine("free(self);");
          CloseBlock();
        }
        else if (klass.IsAllocated) {
          WriteLine();
          Write("static ");
          WriteNew(klass);
        }
      }
    }

    void WriteTypedef(CiClass klass) {
      klass.WriteStatus = CiWriteStatus.NotYet;
      klass.HasFields = klass.Members.Any(member => member is CiField);
      bool klassHasInstanceMethods = klass.Members.Any(member => member is CiMethod && ((CiMethod)member).CallType != CiCallType.Static);
      if (klass.BaseClass != null || klass.HasFields || klassHasInstanceMethods) {
        WriteLine("typedef struct {0} {0};", DecodeSymbol(klass));
      }
    }

    void WriteTypedefs(CiProgram prog, CiVisibility visibility) {
      foreach (CiSymbol symbol in prog.Globals) {
        if (symbol.Visibility == visibility) {
          if (symbol is CiEnum) {
            Translate(symbol);
          }
          else if (symbol is CiClass) {
            WriteTypedef((CiClass)symbol);
          }
          else if (symbol is CiDelegate) {
            ((CiDelegate)symbol).WriteStatus = CiWriteStatus.NotYet;
          }
        }
      }
      foreach (CiSymbol symbol in prog.Globals) {
        if (symbol.Visibility == visibility && symbol is CiDelegate) {
          Translate(symbol);
        }
      }
    }

    static bool AddsVirtualMethods(CiClass klass) {
      return klass.Members.OfType<CiMethod>().Any(method => method.CallType == CiCallType.Abstract || method.CallType == CiCallType.Virtual);
    }

    static CiClass GetVtblStructClass(CiClass klass) {
      while (!AddsVirtualMethods(klass)) {
        klass = klass.BaseClass;
      }
      return klass;
    }

    static CiClass GetVtblPtrClass(CiClass klass) {
      CiClass result = null;
      do {
        if (AddsVirtualMethods(klass)) {
          result = klass;
        }
        klass = klass.BaseClass;
      }
      while (klass != null);
      return result;
    }

    static bool HasVirtualMethods(CiClass klass) {
      // == return EnumVirtualMethods(klass).Any();
      while (!AddsVirtualMethods(klass)) {
        klass = klass.BaseClass;
        if (klass == null) {
          return false;
        }
      }
      return true;
    }

    static IEnumerable<CiMethod> EnumVirtualMethods(CiClass klass) {
      IEnumerable<CiMethod> myMethods = klass.Members.OfType<CiMethod>().Where(method => method.CallType == CiCallType.Abstract || method.CallType == CiCallType.Virtual);
      if (klass.BaseClass != null) {
        return EnumVirtualMethods(klass.BaseClass).Concat(myMethods);
      }
      else {
        return myMethods;
      }
    }

    static bool AddsVirtualMethod(CiClass klass, string methodName) {
      return klass.Members.OfType<CiMethod>().Any(method => method.Name == methodName && (method.CallType == CiCallType.Abstract || method.CallType == CiCallType.Virtual));
    }

    void WritePtr(CiMethod method, string name) {
      StringBuilder sb = new StringBuilder();
      sb.AppendFormat("(*{0})({1} *self", name, DecodeSymbol(method.Class));
      foreach (CiParam param in method.Signature.Params) {
        sb.Append(", ");
        sb.Append(ToString(param.Type, param));
      }
      sb.Append(')');
      CiType type = method.Signature.ReturnType;
      if (method.Throws && type == CiType.Void) {
        // TODO: check subclasses
        type = CiBoolType.Value;
      }
      Write(ToString(type, sb.ToString()));
    }

    void WriteVtblStruct(CiClass klass) {
      if (!AddsVirtualMethods(klass)) {
        return;
      }
      Write("typedef struct ");
      OpenBlock();
      foreach (CiMethod method in EnumVirtualMethods(klass)) {
        WritePtr(method, DecodeSymbol(method));
        WriteLine(";");
      }
      CloseBlock();
      WriteLine("{0}Vtbl;", DecodeSymbol(klass));
    }

    void WriteSignatures(CiClass klass, bool pub) {
      if (!pub && klass.Constructs) {
        WriteConstructorSignature(klass);
        WriteLine(";");
      }
      if (pub && klass.Visibility == CiVisibility.Public && !klass.IsAbstract && HasCStruct(klass)) {
        WriteLine();
        WriteNewSignature(klass);
        WriteLine(";");
        WriteDeleteSignature(klass);
        WriteLine(";");
      }
      foreach (CiSymbol member in klass.Members) {
        if ((member.Visibility == CiVisibility.Public) == pub) {
          if (member is CiConst && pub) {
            Write(klass, (CiConst)member);
          }
          else if (member.Visibility != CiVisibility.Dead) {
            CiMethod method = member as CiMethod;
            if (method != null && method.CallType != CiCallType.Abstract) {
              if (pub) {
                WriteLine();
                WriteDocMethod(method);
              }
              WriteSignature(method);
              WriteLine(";");
            }
          }
        }
      }
    }

    static bool HasVtblValue(CiClass klass) {
      bool result = false;
      foreach (CiSymbol member in klass.Members) {
        CiMethod method = member as CiMethod;
        if (method != null) {
          switch (method.CallType) {
            case CiCallType.Abstract:
              return false;
            case CiCallType.Virtual:
            case CiCallType.Override:
              result = true;
              break;
          }
        }
      }
      return result;
    }

    void WriteVtblValue(CiClass klass) {
      if (!HasVtblValue(klass)) {
        return;
      }
      CiClass structClass = GetVtblStructClass(klass);
      WriteFormat("static const {0}Vtbl CiVtbl_{1} = ", DecodeSymbol(structClass), DecodeSymbol(klass));
      OpenBlock();
      bool first = true;
      foreach (CiMethod method in EnumVirtualMethods(structClass)) {
        CiMethod impl = (CiMethod)klass.Members.Lookup(method);
        if (first) {
          first = false;
        }
        else {
          WriteLine(",");
        }
        if (impl.CallType == CiCallType.Override) {
          Write('(');
          WritePtr(method, string.Empty);
          Write(") ");
        }
        WriteFormat("{0}_{1}", DecodeSymbol(impl.Class), DecodeSymbol(impl));
      }
      WriteLine();
      CloseBlock();
      WriteLine(";");
    }
    // Common pointer sizes are 32-bit and 64-bit.
    // We assume 64-bit, because this avoids mixing pointers and ints
    // which could add extra alignment if pointers are 64-bit.
    const int SizeOfPointer = 8;

    static int SizeOf(CiClass klass) {
      int result = klass.Members.OfType<CiField>().Sum(field => SizeOf(field.Type));
      if (klass.BaseClass != null) {
        result += SizeOf(klass.BaseClass);
      }
      if (GetVtblPtrClass(klass) == klass) {
        result += SizeOfPointer;
      }
      return result;
    }

    static int SizeOf(CiType type) {
      if (type == CiIntType.Value || type == CiBoolType.Value || type is CiEnum) {
        return 4;
      }
      if (type == CiByteType.Value) {
        return 1;
      }
      if (type is CiStringStorageType) {
        return ((CiStringStorageType)type).Length + 1;
      }
      if (type is CiClassStorageType) {
        return SizeOf(((CiClassStorageType)type).Class);
      }
      CiArrayStorageType arrayType = type as CiArrayStorageType;
      if (arrayType != null) {
        return arrayType.Length * SizeOf(arrayType.ElementType);
      }
      return SizeOfPointer;
    }

    void WriteStruct(CiClass klass) {
      // topological sorting of class hierarchy and class storage fields
      if (klass.WriteStatus == CiWriteStatus.Done) {
        return;
      }
      if (klass.WriteStatus == CiWriteStatus.InProgress) {
        throw new ResolveException(klass, "Circular dependency for class {0}");
      }
      klass.WriteStatus = CiWriteStatus.InProgress;
      klass.Constructs = klass.Constructor != null || HasVirtualMethods(klass);
      if (klass.BaseClass != null) {
        WriteStruct(klass.BaseClass);
        klass.Constructs |= klass.BaseClass.Constructs;
      }
      ForEachStorageField(klass, (field, storageClass) => {
        WriteStruct(storageClass);
        klass.Constructs |= storageClass.Constructs;
      });

      klass.WriteStatus = CiWriteStatus.Done;
      WriteLine();
      WriteVtblStruct(klass);
      if (HasCStruct(klass)) {
        WriteFormat("struct {0} ", DecodeSymbol(klass));
        OpenBlock();
        if (klass.BaseClass != null) {
          WriteLine("{0} base;", DecodeSymbol(klass.BaseClass));
        }
        if (GetVtblPtrClass(klass) == klass) {
          WriteLine("const {0}Vtbl *vtbl;", DecodeSymbol(klass));
        }
        IEnumerable<CiField> fields = klass.Members.OfType<CiField>().OrderBy(field => SizeOf(field.Type));
        foreach (CiField field in fields) {
          Translate(field);
        }
        CloseBlock();
        WriteLine(";");
      }
      WriteSignatures(klass, false);
      WriteVtblValue(klass);
      foreach (CiConst konst in klass.ConstArrays) {
        if (konst.Class != null) {
          if (konst.Visibility != CiVisibility.Public) {
            Write("static ");
          }
          WriteLine("const {0} = {1};", ToString(konst.Type, konst), DecodeValue(konst.Type, konst.Value));
        }
      }
      foreach (CiBinaryResource resource in klass.BinaryResources) {
        WriteLine("static const unsigned char {0}[{1}] = {2};", DecodeSymbol(resource), resource.Content.Length, DecodeValue(resource.Type, resource.Content));
      }
    }

    void WriteCode(CiClass klass) {
      WriteConstructorNewDelete(klass);
      foreach (CiSymbol member in klass.Members) {
        if (member is CiMethod) {
          Translate(member);
        }
      }
    }

    void WriteGuard(string directive) {
      WriteFormat("{0} _", directive);
      foreach (char c in Path.GetFileNameWithoutExtension(this.OutputFile)) {
        Write(CiLexer.IsLetter(c) ? char.ToUpperInvariant(c) : '_');
      }
      WriteLine("_H_");
    }

    protected virtual void WriteBoolType() {
      WriteLine("#include <stdbool.h>");
    }

    #region CiTo Library handlers
    public override void Library_SByte(CiPropertyAccess expr) {
      Write("(signed char) ");
      WriteChild(expr, expr.Obj);
    }

    public override void Library_LowByte(CiPropertyAccess expr) {
      Write("(unsigned char) ");
      WriteChild(expr, expr.Obj);
    }

    public override void Library_Length(CiPropertyAccess expr) {
      Write("(int) strlen(");
      WriteChild(expr, expr.Obj);
      Write(')');
    }

    public override void Library_MulDiv(CiMethodCall expr) {
      Write("(int) ((long long int) ");
      WriteChild(CiPriority.Prefix, expr.Obj);
      Write(" * ");
      WriteChild(CiPriority.Multiplicative, expr.Arguments[0]);
      Write(" / ");
      WriteChild(CiPriority.Multiplicative, expr.Arguments[1], true);
      Write(")");
    }

    public override void Library_CharAt(CiMethodCall expr) {
      Translate(expr.Obj);
      Write('[');
      Translate(expr.Arguments[0]);
      Write(']');
    }

    public override void Library_Substring(CiMethodCall expr) {
      // TODO
      throw new ArgumentException("Substring");
    }

    public override void Library_CopyTo(CiMethodCall expr) {
      Write("memcpy(");
      WriteSum(expr.Arguments[1], expr.Arguments[2]);
      Write(", ");
      WriteSum(expr.Obj, expr.Arguments[0]);
      Write(", ");
      Translate(expr.Arguments[3]);
      Write(')');
    }

    public override void Library_ToString(CiMethodCall expr) {
      // TODO
      throw new ArgumentException("Array.ToString");
    }

    public override void Library_Clear(CiMethodCall expr) {
      WriteClearArray(expr.Obj);
    }
    #endregion
  }
}

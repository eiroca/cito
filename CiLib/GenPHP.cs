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
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace Foxoft.Ci {

  public class GenPHP : CiGenerator {
    public GenPHP(string aNamespace) : base(aNamespace) {
    }

    public GenPHP() : base() {
      CommentCodeBegin = "<c>";
      CommentCodeEnd = "</c>";
      CommentListBegin = "<list type=\"bullet\">";
      CommentListEnd = "</list>";
      CommentItemListBegin = " <item>";
      CommentItemListEnd = "</item>";
      CommentSummaryBegin = "<summary>";
      CommentSummaryEnd = "</summary>";
      CommentBeginStr = "/**";
      CommentEndStr = " */";
      CommentContinueStr = " * ";
      CommentRemarkBegin = "<remarks>";
      CommentRemarkEnd = "</remarks>";
    }

    #region Converter specialization
    public override void Expression_CiConstAccess(CiExpr expression) {
      CiConstAccess expr = (CiConstAccess)expression;
      WriteFormat("$this->{0}", DecodeSymbol(expr.Const));
    }

    public override void Expression_CiMethodCall(CiExpr expression) {
      CiMethodCall expr = (CiMethodCall)expression;
      if (!Translate(expr)) {
        if (expr.Method != null) {
          if (expr.Obj != null) {
            Translate(expr.Obj);
          }
          else {
            Write(DecodeSymbol(expr.Method.Class));
          }
          if (expr.Method.CallType == CiCallType.Static) {
            Write("::");
          }
          else {
            Write("->");
          }
          Write(DecodeSymbol(expr.Method));
        }
        else {
          Translate(expr.Obj);
        }
        WriteArguments(expr);
      }
    }

    public override void Expression_CiNewExpr(CiExpr expression) {
      CiNewExpr expr = (CiNewExpr)expression;
      WriteNew(expr.NewType);
    }

    public override void Expression_CiCoercion(CiExpr expression) {
      CiCoercion expr = (CiCoercion)expression;
      if (expr.ResultType == CiByteType.Value && expr.Inner.Type == CiIntType.Value) {
        Write("");
        WriteChild(expr, (CiExpr)expr.Inner); 
      }
      else {
        WriteInline(expr.Inner);
      }
    }

    public override void Expression_CiFieldAccess(CiExpr expression) {
      CiFieldAccess expr = (CiFieldAccess)expression;
      WriteChild(expr, expr.Obj);
      Write("->");
      Write(DecodeSymbol(expr.Field));
    }

    public override void Expression_CiVarAccess(CiExpr expression) {
      CiVarAccess expr = (CiVarAccess)expression;
      Write("$");
      Write(DecodeSymbol(expr.Var));
    }

    public override void Statement_CiThrow(ICiStatement statement) {
      CiThrow stmt = (CiThrow)statement;
      Write("throw new Exception(");
      Translate(stmt.Message);
      WriteLine(");");
    }

    public override void Statement_CiVar(ICiStatement statement) {
      CiVar stmt = (CiVar)statement;
      if (stmt.Type is CiArrayStorageType) {
        if (stmt.InitialValue != null) {
          WriteFormat("${0} = array_fill(0, ", DecodeSymbol(stmt));
          CiArrayStorageType storageType = (CiArrayStorageType)stmt.Type;
          if (storageType.LengthExpr != null) {
            Translate(storageType.LengthExpr);
          }
          else {
            Write(storageType.Length);
          }
          Write(", ");
          Translate(stmt.InitialValue);
          Write(")");
        }
        else {
          WriteFormat("${0} = array()", DecodeSymbol(stmt));
          WriteInit(stmt.Type);
        }
      }
      else {
        bool sep = false;
        if (stmt.Type is CiClassStorageType) {
          WriteFormat("${0}", DecodeSymbol(stmt));
          sep = true;
          WriteInit(stmt.Type);
        }
        if (stmt.InitialValue != null) {
          if (sep == true) {
            WriteLine(";");
          }
          WriteFormat("${0} = ", DecodeSymbol(stmt));
          Translate(stmt.InitialValue);
        }
      }
    }

    public override void Symbol_CiConst(CiSymbol symbol) {
      CiConst konst = (CiConst)symbol;
      WriteDocCode(konst.Documentation);
      WriteLine("const {0} = {1};", DecodeSymbol(konst), DecodeValue(konst.Type, konst.Value));
    }

    public override void Symbol_CiEnum(CiSymbol symbol) {
      CiEnum enu = (CiEnum)symbol;
      WriteLine();
      WriteDocCode(enu.Documentation);
      int i = 0;
      foreach (CiEnumValue value in enu.Values) {
        WriteDocCode(value.Documentation);
        WriteLine("define('{0}_{1}', {2});", DecodeSymbol(enu), DecodeSymbol(value), i);
        i++;
      }
    }

    public override void Symbol_CiField(CiSymbol symbol) {
      CiField field = (CiField)symbol;
      WriteDocCode(field.Documentation);
      WriteLine("var ${0};", DecodeSymbol(field));
    }

    public override void Symbol_CiClass(CiSymbol symbol) {
      CiClass klass = (CiClass)symbol;
      WriteLine();
      WriteDocCode(klass.Documentation);
      OpenClass(klass.IsAbstract, klass, " extends ");
      foreach (CiConst konst in klass.ConstArrays) {
        WriteLine("var ${0} = {1};", DecodeSymbol(konst), DecodeValue(konst.Type, konst.Value));
      }
      foreach (CiBinaryResource resource in klass.BinaryResources) {
        WriteLine("var ${0} = {1};", DecodeSymbol(resource), DecodeValue(null, resource.Content));
      }
      foreach (CiSymbol member in klass.Members) {
        if (member is CiField) {
          Translate(member);
        }
      }
      Write("function __constructor() ");
      OpenBlock();
      foreach (CiSymbol member in klass.Members) {
        if ((member is CiField)) {
          CiField f = (CiField)member;
          if ((f.Type is CiClassStorageType) || (f.Type is CiArrayStorageType)) {
            WriteFormat("$this->{0}", DecodeSymbol(f));
            WriteInit(f.Type);
            WriteLine(";");
          }
        }
      }
      if (klass.Constructor != null) {
        if (klass.Constructor.Body is CiBlock) {
          WriteCode(((CiBlock)klass.Constructor.Body).Statements);
        }
        else {
          WriteCode(klass.Constructor.Body);
        }
      }
      CloseBlock();
      foreach (CiSymbol member in klass.Members) {
        if (!(member is CiField)) {
          Translate(member);
        }
      }
      CloseBlock();
    }

    public override void Symbol_CiDelegate(CiSymbol symbol) {
      CiDelegate del = (CiDelegate)symbol;
      WriteDocCode(del.Documentation);
      Write("// delegate ");
      WriteSignature(del);
      WriteLine(";");
    }

    public override void Symbol_CiMethod(CiSymbol symbol) {
      CiMethod method = (CiMethod)symbol;
      WriteLine();
      WriteDocCode(method.Documentation);
      foreach (CiParam param in method.Signature.Params) {
        if (param.Documentation != null) {
          WriteFormat("/** <param name=\"{0}\">", DecodeSymbol(param));
          WriteDocPara(param.Documentation.Summary);
          WriteLine("</param> */");
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
          callType = "";
          break;
        case CiCallType.Override:
          callType = "";
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
    #endregion

    #region CiTo Library handlers
    public override void Library_SByte(CiPropertyAccess expr) {
      Write("");
      WriteChild(expr, expr.Obj);
    }

    public override void Library_LowByte(CiPropertyAccess expr) {
      Write("");
      WriteChild(expr, expr.Obj);
    }

    public override void Library_Length(CiPropertyAccess expr) {
      Write("strlen(");
      WriteChild(expr, expr.Obj);
      Write(")");
    }

    public override void Library_MulDiv(CiMethodCall expr) {
      WriteChild(CiPriority.Prefix, expr.Obj);
      Write(" * ");
      WriteChild(CiPriority.Multiplicative, expr.Arguments[0]);
      Write(" / ");
      WriteChild(CiPriority.Multiplicative, expr.Arguments[1], true);
    }

    public override void Library_CharAt(CiMethodCall expr) {
      Write("(int)");
      Translate(expr.Obj);
      Write("[");
      Translate(expr.Arguments[0]);
      Write("]");
    }

    public override void Library_Substring(CiMethodCall expr) {
      Write("substr(");
      Translate(expr.Obj);
      Write(", ");
      Translate(expr.Arguments[0]);
      Write(", ");
      Translate(expr.Arguments[1]);
      Write(')');
    }

    public override void Library_CopyTo(CiMethodCall expr) {
      UseFunction("CopyTo");
      Write("Ci::CopyTo(");
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

    public override void Library_ToString(CiMethodCall expr) {
      UseFunction("ToString");
      Write("Ci::ToString(");
      Translate(expr.Obj);
      Write(", ");
      Translate(expr.Arguments[0]);
      Write(", ");
      Translate(expr.Arguments[1]);
      Write(')');
    }

    public override void Library_Clear(CiMethodCall expr) {
      UseFunction("Clear");
      Write("Ci::Clear(");
      Translate(expr.Obj);
      Write(", 0, ");
      Write(((CiArrayStorageType)expr.Obj.Type).Length);
      Write(')');
    }
    #endregion

    void Write(CiVisibility visibility) {
      switch (visibility) {
        case CiVisibility.Dead:
        case CiVisibility.Private:
          break;
        case CiVisibility.Internal:
          break;
        case CiVisibility.Public:
          Write("public ");
          break;
      }
    }

    public override void WriteNew(CiType type) {
      CiClassStorageType classType = type as CiClassStorageType;
      if (classType != null) {
        WriteFormat("new {0}()", DecodeSymbol(classType.Class));
      }
      else {
        // CiArrayStorageType arrayType = (CiArrayStorageType) type;
        Write("array()");
      }
    }

    protected override void WriteFallthrough(CiExpr expr) {
      Write("//$FALL-THROUGH$ go to ");
      if (expr != null) {
        Translate(expr);
      }
      else {
        Write("default");
      }
      WriteLine(":");
    }

    public override void EmitProgram(CiProgram prog) {
      ClearUsedFunction();
      CreateFile(this.OutputFile);
      if (this.Namespace != null) {
        WriteLine("namespace {0};", this.Namespace);
      }
      foreach (CiSymbol symbol in prog.Globals) {
        if (symbol is CiEnum) {
          Translate(symbol);
        }
      }
      foreach (CiSymbol symbol in prog.Globals) {
        if (!(symbol is CiEnum)) {
          Translate(symbol);
        }
      }
      if (HasUsedFunction()) {
        Write("class Ci ");
        OpenBlock();
        if (IsUsedFunction("Clear")) {
          WriteLine("static function Clear(&$arr, $v, $len) {for($i=0;$i<$len;$i++) {$arr[$i]=$v;}}");
        }
        if (IsUsedFunction("CopyTo")) {
          WriteLine("static function CopyTo(&$src, $src_strt, &$dest, $dst_strt, $dst_len) {for($i=0;$i<$dst_len;$i++) {$dst[$dst_strt+$i]=$src[$src_strt+$i];}}");
        }
        if (IsUsedFunction("ToString")) {
          WriteLine("static function ToString(&$src, $src_strt, $src_len) {$r=''; for($i=$src_strt;$i<$src_strt+$src_len;$i++) {$r .= chr($src[$i]);} return $r}");
        }
        CloseBlock();
      }
      CloseFile();
    }

    protected override void WriteBanner() {
      WriteLine("<?php");
      WriteLine("// Generated automatically with \"cito\". Do not edit.");
    }

    protected override void  WriteFooter() {
      WriteLine("?>");
    }

    protected override void OpenClass(bool isAbstract, CiClass klass, string extendsClause) {
      if (isAbstract) {
        Write("abstract ");
      }
      WriteFormat("class {0}", DecodeSymbol(klass));
      if (klass.BaseClass != null) {
        Write(extendsClause);
        Write(DecodeSymbol(klass.BaseClass));
      }
      Write(" ");
      OpenBlock();
    }

    public override void WriteSignature(CiDelegate del) {
      WriteFormat("function {0}(", DecodeSymbol(del));
      bool first = true;
      foreach (CiParam param in del.Params) {
        if (first) {
          first = false;
        }
        else {
          Write(", ");
        }
        if ((param.Type is CiClassType) || (param.Type is CiArrayType)) {
          Write("&");
        }
        WriteFormat("${0}", DecodeSymbol(param));
      }
      Write(')');
    }

    public override bool WriteInit(CiType type) {
      if (type is CiClassStorageType || type is CiArrayStorageType) {
        Write(" = ");
        WriteNew(type);
        return true;
      }
      return false;
    }

    public override string DecodeValue(CiType type, object value) {
      StringBuilder res = new StringBuilder();
      if (value is CiEnumValue) {
        CiEnumValue ev = (CiEnumValue)value;
        res.AppendFormat("{0}_{1}", DecodeSymbol(ev.Type), DecodeSymbol(ev));
      }
      else if (value is Array) {
        res.Append("array( ");
        res.Append(DecodeArray(type, (Array)value));
        res.Append(" )");
      }
      else {
        res.Append(base.DecodeValue(type, value));
      }
      return res.ToString();
    }
  }
}
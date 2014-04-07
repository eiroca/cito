// GenPerl5.cs - Perl 5 code generator
//
// Copyright (C) 2013  Piotr Fusik
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
using System.Linq;
using System.Text;

namespace Foxoft.Ci {

  public abstract class GenPerl5 : CiGenerator {
    string Package;
    public bool USE_INTEGER = true;

    public GenPerl5(string aNamespace) : this() {
      SetNamespace(aNamespace);
    }

    public GenPerl5() : base() {
      Package = string.Empty;
      Namespace = String.Empty;
      CommentContinueStr = "";
      CommentBeginStr = "";
      CommentEndStr = "";
      CommentSpecialChar.Clear();
      CommentSpecialChar.Add('<', "E<lt>");
      CommentSpecialChar.Add('>', "E<gt>");
      CommentCodeBegin = "C<";
      CommentCodeEnd = ">";
      CommentListBegin = "=over" + NewLineStr;
      CommentListEnd = "=back" + NewLineStr;
      CommentItemListBegin = "=item *" + NewLineStr;
      CommentItemListEnd = "";
      CommentSummaryBegin = "";
      CommentSummaryEnd = "";
      CommentRemarkBegin = "";
      CommentRemarkEnd = "";
      Decode_TRUEVALUE = "1";
      Decode_FALSEVALUE = "0";
      Decode_ENUMFORMAT = "{0}::{1}()";
      Decode_ARRAYBEGIN = "( ";
      Decode_ARRAYEND = " )";
      Decode_NULLVALUE = "undef";
      Decode_SPECIALCHAR.Add('$', "\\$");
      Decode_SPECIALCHAR.Add('@', "\\@");
      SimpleCommentFormat = "#{0}";
      TranslateSymbolName = Perl_SymbolNameTranslator;
      ForceBraceForSingleStatement = true;
    }

    public string Perl_SymbolNameTranslator(CiSymbol aSymbol) {
      if (aSymbol == null) {
        return "";
      }
      String name = aSymbol.Name;
      if (aSymbol is CiEnumValue) {
        name = ToUppercaseWithUnderscores(name);
      }
      else if (aSymbol is CiConst) {
        name = ToUppercaseWithUnderscores(name);
      }
      else if (aSymbol is CiField) {
        name = ToLowercaseWithUnderscores(name);
      }
      else if (aSymbol is CiMethod) {
        name = ToLowercaseWithUnderscores(name);
      }
      else if (aSymbol is CiClass) {
        name = ToLowercase(name);
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

    public override void SetNamespace(string aNamespace) {
      base.SetNamespace(aNamespace);
      this.Package = aNamespace == null ? string.Empty : aNamespace + "::";
    }

    public override void EmitProgram(CiProgram prog) {
      CreateFile(this.OutputFile);
      WritePragmas(prog);
      if (USE_INTEGER) {
        WriteLine("use integer;");
      }
      WriteLine("use strict;");
      WriteLine();
      // Write enums first, otherwise
      // given (foo) { when (Enum::VALUE()) { ... } }
      // won't work as expected.
      foreach (CiSymbol symbol in prog.Globals) {
        if (symbol is CiEnum) {
          Symbol_CiEnum(symbol);
        }
      }
      foreach (CiSymbol symbol in prog.Globals) {
        if (symbol is CiClass) {
          Symbol_CiClass(symbol);
        }
      }
      WriteLine("1;");
      CloseFile();
    }

    public override CiPriority GetPriority(CiExpr expr) {
      if (expr is CiPropertyAccess) {
        CiProperty prop = ((CiPropertyAccess)expr).Property;
        if (prop == CiLibrary.SByteProperty) {
          return CiPriority.Additive;
        }
        if (prop == CiLibrary.LowByteProperty) {
          return CiPriority.And;
        }
      }
      else if (!USE_INTEGER) {
        if (expr is CiBinaryExpr) {
          if (((CiBinaryExpr)expr).Op == CiToken.Slash) {
            return CiPriority.Postfix;
          }
        }
      }
      return base.GetPriority(expr);
    }

    #region Converter Symbols
    public override void Symbol_CiEnum(CiSymbol symbol) {
      CiEnum enu = (CiEnum)symbol;
      if (enu.Visibility == CiVisibility.Public) {
        WriteLine("=head1 Enum {0}{1}", this.Package, DecodeSymbol(enu));
        WriteLine();
        WriteDocCode(enu.Documentation);
        foreach (CiEnumValue value in enu.Values) {
          WriteConstDoc(enu, value);
        }
        WriteLine("=cut");
        WriteLine();
      }
      WritePackage(enu);
      for (int i = 0; i < enu.Values.Length; i++) {
        WriteConst(enu.Values[i], i);
      }
      WriteLine();
    }

    public override void Symbol_CiClass(CiSymbol symbol) {
      CiClass klass = (CiClass)symbol;
      if (klass.Visibility == CiVisibility.Public) {
        WriteLine("=head1 Class {0}{1}", this.Package, DecodeSymbol(klass));
        WriteLine();
        WriteDocCode(klass.Documentation);
        WriteLine("=cut");
        WriteLine();
      }
      WritePackage(klass);
      if (klass.BaseClass != null) {
        WriteLine("our @ISA = '{0}{1}';", this.Package, DecodeSymbol(klass.BaseClass));
      }
      foreach (CiSymbol member in klass.Members) {
        if (member.Visibility == CiVisibility.Public) {
          CiConst konst = member as CiConst;
          if (konst != null) {
            WriteConstDoc(klass, konst);
            WriteLine("=cut");
            WriteLine();
            WriteConst(konst, konst.Value);
            WriteLine();
          }
        }
      }
      foreach (CiConst konst in klass.ConstArrays) {
        WriteLine("our @{0} = {1};", DecodeSymbol(konst), DecodeValue(konst.Type, konst.Value));
        WriteLine();
      }
      foreach (CiBinaryResource resource in klass.BinaryResources) {
        WriteLine("our @{0} = {1};", DecodeSymbol(resource), DecodeValue(resource.Type, resource.Content));
        WriteLine();
      }
      WriteConstructor(klass);
      foreach (CiSymbol member in klass.Members) if (member is CiMethod) {
          Translate(member);
        }
    }
    #endregion

    #region Converter Expression
    public override void Expression_CiVarAccess(CiExpr expression) {
      CiVarAccess expr = (CiVarAccess)expression;
      Write('$');
      if (expr.Var == CurrentMethod().This) {
        Write("self");
      }
      else {
        Write(DecodeSymbol(expr.Var));
      }
    }

    public override void Expression_CiFieldAccess(CiExpr expression) {
      CiFieldAccess expr = (CiFieldAccess)expression;
      WriteChild(CiPriority.Postfix, expr.Obj);
      WriteFormat("->{{{0}}}", DecodeSymbol(expr.Field));
    }

    public override void Expression_CiArrayAccess(CiExpr expression) {
      CiArrayAccess expr = (CiArrayAccess)expression;
      if (expr.Array is CiConstAccess || expr.Array is CiBinaryResourceExpr) {
        Write('$');
      }
      WriteChild(expr, expr.Array);
      if (expr.Array.Type is CiArrayPtrType) {
        Write("->");
      }
      Write('[');
      Translate(expr.Index);
      Write(']');
    }

    public override void Expression_CiMethodCall(CiExpr expression) {
      CiMethodCall expr = (CiMethodCall)expression;
      if (!Translate(expr)) {
        if (expr.Method != null) {
          if (expr.Obj != null) {
            Translate(expr.Obj);
            Write("->");
          }
          else {
            WriteFormat("{0}{1}::", this.Package, DecodeSymbol(expr.Method.Class));
          }
          Write(DecodeSymbol(expr.Method));
        }
        else {
          // delegate call
          Translate(expr.Obj);
          Write("->");
        }
        WriteArguments(expr);
      }
    }

    public override void Expression_CiCoercion(CiExpr expression) {
      CiCoercion expr = (CiCoercion)expression;
      if (expr.Inner.Type is CiArrayStorageType && WritePerlArray("\\@", expr.Inner)) {
        // ok: \@var, \@const, \@binaryResource
      }
      else {
        base.Expression_CiCoercion(expr);
      }
    }

    public override void Expression_CiBinaryExpr(CiExpr expression) {
      CiBinaryExpr expr = (CiBinaryExpr)expression;
      switch (expr.Op) {
        case CiToken.Equal:
        case CiToken.NotEqual:
          if (expr.Left.IsConst(null)) {
            // null != thing -> defined(thing)
            // null == thing -> !defined(thing)
            WriteDefined((expr.Op == CiToken.Equal) ? "!" : "", expr.Right);
          }
          else if (expr.Right.IsConst(null)) {
            // thing != null -> defined(thing)
            // thing == null -> !defined(thing)
            WriteDefined((expr.Op == CiToken.Equal) ? "!" : "", expr.Left);
          }
          else if (expr.Left.Type is CiStringType) {
            WriteChild(expr, expr.Left);
            Write(expr.Op == CiToken.Equal ? " eq " : " ne ");
            WriteChild(expr, expr.Right);
          }
          else {
            base.Expression_CiBinaryExpr(expr);
          }
          break;
        case CiToken.Slash:
          if (!USE_INTEGER) {
            Write("int(");
            WriteChild(CiPriority.Multiplicative, expr.Left);
            Write(" / ");
            WriteChild(CiPriority.Multiplicative, expr.Right, true);
            Write(')');
          }
          else {
            base.Expression_CiBinaryExpr(expr);
          }
          break;
        default:
          base.Expression_CiBinaryExpr(expr);
          break;
      }
    }
    #endregion

    #region Converter Statements
    public override void Statement_CiBlock(ICiStatement statement) {
      CiBlock block = (CiBlock)statement;
      // Avoid blocks, as they count as loops for last/next.
      // At worst we'll get warning about duplicate "my" declarations.
      WriteCode(block.Statements);

    }

    public override void Statement_CiAssign(ICiStatement statement) {
      CiAssign assign = (CiAssign)statement;
      if (assign.Op == CiToken.AddAssign && assign.Target.Type is CiStringStorageType) {
        Translate(assign.Target);
        Write(" .= ");
        WriteInline(assign.Source);
      }
      else {
        base.Statement_CiAssign(assign);
      }
    }

    protected bool BreakDoWhile = false;

    public override void Statement_CiBreak(ICiStatement statement) {
      if (this.BreakDoWhile) {
        WriteLine("last DOWHILE;");
      }
      else {
        WriteLine("last;");
      }
    }

    public override void Statement_CiFor(ICiStatement statement) {
      CiFor stmt = (CiFor)statement;
      bool oldBreakDoWhile = this.BreakDoWhile;
      this.BreakDoWhile = false;
      WriteLoopLabel(stmt);
      base.Statement_CiFor(stmt);
      this.BreakDoWhile = oldBreakDoWhile;
    }

    public override void Statement_CiWhile(ICiStatement statement) {
      CiWhile stmt = (CiWhile)statement;
      bool oldBreakDoWhile = this.BreakDoWhile;
      this.BreakDoWhile = false;
      WriteLoopLabel(stmt);
      base.Statement_CiWhile(stmt);
      this.BreakDoWhile = oldBreakDoWhile;
    }

    public override void Statement_CiIf(ICiStatement statement) {
      CiIf stmt = (CiIf)statement;
      Write("if (");
      Translate(stmt.Cond);
      Write(')');
      WriteChild(stmt.OnTrue);
      if (stmt.OnFalse != null) {
        Write("els");
        if (stmt.OnFalse is CiIf) {
          Translate(stmt.OnFalse);
        }
        else {
          Write('e');
          WriteChild(stmt.OnFalse);
        }
      }
    }

    public override void Statement_CiVar(ICiStatement statement) {
      CiVar stmt = (CiVar)statement;
      Write("my ");
      Write(stmt.Type is CiArrayStorageType ? '@' : '$');
      Write(stmt.Name);
      if (stmt.InitialValue != null) {
        Write(" = ");
        if (stmt.Type is CiArrayStorageType) {
          Write("(0) x ");
          Write(((CiArrayStorageType)stmt.Type).Length);
        }
        else {
          Translate(stmt.InitialValue);
        }
      }
      else if (stmt.Type is CiClassStorageType) {
        Write(" = ");
        WriteNew(stmt.Type);
      }
    }

    public override void Statement_CiThrow(ICiStatement statement) {
      CiThrow stmt = (CiThrow)statement;
      Write("die ");
      Translate(stmt.Message);
      WriteLine(";");
    }

    public override void Statement_CiDoWhile(ICiStatement statement) {
      CiDoWhile stmt = (CiDoWhile)statement;
      bool hasBreak = HasBreak(stmt.Body);
      bool hasContinue = HasContinue(stmt.Body);
      bool oldBreakDoWhile = this.BreakDoWhile;
      if (hasBreak) {
        // { do { ... last; ... } while cond; }
        if (hasContinue) {
          // DOWHILE: { do { { ... last DOWHILE; ... next; ... } } while cond; }
          this.BreakDoWhile = true;
          Write("DOWHILE: ");
        }
        OpenBlock();
      }
      Write("do");
      if (hasContinue) {
        // do { { ... next; ... } } while cond;
        Write(' ');
        OpenBlock();
        WriteLoopLabel(stmt);
        WriteChild(stmt.Body);
        CloseBlock();
      }
      else {
        WriteChild(stmt.Body);
      }
      Write("while ");
      Translate(stmt.Cond);
      WriteLine(";");
      if (hasBreak) {
        this.BreakDoWhile = oldBreakDoWhile;
        CloseBlock();
      }
    }

    public override void Symbol_CiMethod(CiSymbol symbol) {
      CiMethod method = (CiMethod)symbol;
      if (method.Visibility == CiVisibility.Public) {
        Write("=head2 C<");
        if (method.CallType == CiCallType.Static) {
          WriteFormat("{0}{1}::", this.Package, DecodeSymbol(method.Class));
        }
        else {
          WriteFormat("${0}-E<gt>", DecodeSymbol(method.Class));
        }
        WriteFormat("{0}(", DecodeSymbol(method));
        bool first = true;
        foreach (CiParam param in method.Signature.Params) {
          if (first) {
            first = false;
          }
          else {
            Write(", ");
          }
          WriteDocName(param);
        }
        WriteLine(")>");
        WriteLine();
        WriteDocCode(method.Documentation);
        if (method.Signature.Params.Any(param => param.Documentation != null)) {
          WriteLine("Parameters:");
          WriteLine();
          WriteLine("=over");
          WriteLine();
          foreach (CiParam param in method.Signature.Params) {
            Write("=item ");
            WriteDocName(param);
            WriteLine();
            WriteLine();
            WriteDocCode(param.Documentation);
          }
          WriteLine("=back");
          WriteLine();
        }
        WriteLine("=cut");
        WriteLine();
      }
      if (method.CallType == CiCallType.Abstract) {
        return;
      }
      EnterMethod(method);
      WriteFormat("sub {0}(", DecodeSymbol(method));
      if (method.CallType != CiCallType.Static) {
        Write('$');
      }
      foreach (CiParam param in method.Signature.Params) {
        Write('$');
      }
      Write(") ");
      OpenBlock();
      if (method.CallType != CiCallType.Static || method.Signature.Params.Length > 0) {
        Write("my (");
        bool first = true;
        if (method.CallType != CiCallType.Static) {
          Write("$self");
          first = false;
        }
        foreach (CiParam param in method.Signature.Params) {
          if (first) {
            first = false;
          }
          else {
            Write(", ");
          }
          Write('$');
          Write(param.Name);
        }
        WriteLine(") = @_;");
      }
      WriteCode(method.Body.Statements);
      CloseBlock();
      WriteLine();
      ExitMethod();
    }

    public override void Statement_CiDelete(ICiStatement statement) {
    }
    #endregion

    void WriteConstDoc(CiSymbol parent, CiSymbol child) {
      WriteLine("=head2 C<{0}{1}::{2}()>", this.Package, DecodeSymbol(parent), DecodeSymbol(child));
      WriteLine();
      WriteDocCode(child.Documentation);
    }

    void WritePackage(CiSymbol symbol) {
      WriteLine("package {0}{1};", this.Package, DecodeSymbol(symbol));
      WriteLine();
    }

    void WriteConst(CiSymbol name, object value) {
      WriteLine("sub {0}() {{ {1} }}", DecodeSymbol(name), DecodeValue(null, value));
    }

    bool WritePerlArray(string sigil, CiMaybeAssign expr) {
      bool isVar = expr is CiVarAccess;
      if (isVar || expr is CiConstAccess || expr is CiBinaryResourceExpr) {
        Write(sigil);
        if (isVar) {
          Write(((CiVarAccess)expr).Var.Name);
        }
        else {
          Translate((CiExpr)expr);
        }
        return true;
      }
      return false;
    }

    void WriteSlice(CiExpr array, CiExpr index, CiExpr lenMinus1) {
      if (array is CiCoercion && WritePerlArray("@", ((CiCoercion)array).Inner)) {
        // ok: @var, @const, @binaryResource
      }
      else if (array.Type is CiArrayStorageType && WritePerlArray("@", array)) {
        // ok: @var, @const, @binaryResource
      }
      else {
        Write("@{");
        Translate(array);
        Write('}');
      }
      Write('[');
      Translate(index);
      Write(" .. ");
      WriteSum(index, lenMinus1);
      Write(']');
    }

    void WriteDefined(string prefix, CiExpr expr) {
      Write(prefix);
      Write("defined(");
      Translate(expr);
      Write(')');
    }

    public override void WriteNew(CiType type) {
      CiClassStorageType classType = type as CiClassStorageType;
      if (classType != null) {
        WriteFormat("{0}{1}->new()", this.Package, DecodeSymbol(classType.Class));
      }
      else Write("[]"); // new array reference
    }

    protected override void WriteChild(ICiStatement stmt) {
      Write(' ');
      OpenBlock();
      Translate(stmt);
      if (curLine.Length > 0) {
        WriteLine(";");
      }
      CloseBlock();
    }

    protected static bool HasBreak(ICiStatement stmt) {
      // note: support stmt==null from ifStmt.OnFalse
      if (stmt is CiBreak) {
        return true;
      }
      CiIf ifStmt = stmt as CiIf;
      if (ifStmt != null) {
        return HasBreak(ifStmt.OnTrue) || HasBreak(ifStmt.OnFalse);
      }
      CiBlock block = stmt as CiBlock;
      if (block != null) {
        return block.Statements.Any(s => HasBreak(s));
      }
      return false;
    }

    protected static bool HasContinue(ICiStatement stmt) {
      // note: support stmt==null from ifStmt.OnFalse
      if (stmt is CiContinue) {
        return true;
      }
      CiIf ifStmt = stmt as CiIf;
      if (ifStmt != null) {
        return HasContinue(ifStmt.OnTrue) || HasContinue(ifStmt.OnFalse);
      }
      CiBlock block = stmt as CiBlock;
      if (block != null) {
        return block.Statements.Any(s => HasContinue(s));
      }
      CiSwitch switchStmt = stmt as CiSwitch;
      if (switchStmt != null) {
        return switchStmt.Cases.Any(kase => kase.Body.Any(s => HasContinue(s)));
      }
      return false;
    }

    protected virtual void WriteLoopLabel(CiLoop stmt) {
    }

    protected static int BodyLengthWithoutLastBreak(ICiStatement[] body) {
      int length = body.Length;
      if (length > 0 && body[length - 1] is CiBreak) {
        return length - 1;
      }
      return length;
    }

    void WriteDocName(CiParam param) {
      WriteFormat("{0}{1}", (param.Type is CiArrayType) ? "\\@" : "$", DecodeSymbol(param));
    }

    void WriteConstructor(CiClass klass) {
      // TODO: skip constructor if static methods only?
      if (klass.Visibility == CiVisibility.Public) {
        WriteLine("=head2 C<${0} = {1}{2}-E<gt>new()>", DecodeSymbol(klass), this.Package, DecodeSymbol(klass));
        WriteLine();
        if (klass.Constructor != null) {
          WriteDocCode(klass.Constructor.Documentation);
        }
        WriteLine("=cut");
        WriteLine();
      }
      IEnumerable<CiField> classStorageFields = klass.Members.OfType<CiField>().Where(field => field.Type is CiClassStorageType);
      if (klass.Constructor == null && klass.BaseClass != null && !classStorageFields.Any()) {
        // base constructor does the job
        return;
      }
      Write("sub new($) ");
      OpenBlock();
      if (klass.BaseClass != null) {
        WriteLine("my $self = shift()->SUPER::new();");
      }
      else {
        WriteLine("my $self = bless {}, shift;");
      }
      foreach (CiField field in classStorageFields) {
        WriteFormat("$self->{{{0}}} = ", DecodeSymbol(field));
        WriteNew(field.Type);
        WriteLine(";");
      }
      if (klass.Constructor != null) {
        EnterMethod(klass.Constructor);
        WriteCode(klass.Constructor.Body.Statements);
        ExitMethod();
      }
      WriteLine("return $self;"); // TODO: premature returns
      CloseBlock();
      WriteLine();
    }

    protected virtual void WritePragmas(CiProgram prog) {
    }

    #region CiTo Library handlers
    public override void Library_SByte(CiPropertyAccess expr) {
      Write('(');
      WriteChild(CiPriority.Xor, expr.Obj);
      Write(" ^ 128) - 128");
    }

    public override void Library_LowByte(CiPropertyAccess expr) {
      WriteChild(expr, expr.Obj);
      Write(" & 0xff");
    }

    public override void Library_Length(CiPropertyAccess expr) {
      Write("length(");
      Translate(expr.Obj);
      Write(')');
    }

    public override void Library_MulDiv(CiMethodCall expr) {
      if (USE_INTEGER) {
        // FIXME: overflow on 32-bit perl
        Write("(");
      }
      else {
        Write("int(");
      }
      WriteChild(CiPriority.Prefix, expr.Obj);
      Write(" * ");
      WriteChild(CiPriority.Multiplicative, expr.Arguments[0]);
      Write(" / ");
      WriteChild(CiPriority.Multiplicative, expr.Arguments[1], true);
      Write(")");
    }

    public override void Library_CharAt(CiMethodCall expr) {
      Write("ord(substr(");
      Translate(expr.Obj);
      Write(", ");
      Translate(expr.Arguments[0]);
      Write(", 1))");
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
      CiExpr lenMinus1 = new CiBinaryExpr(expr.Arguments[3], CiToken.Minus, new CiConstExpr(1));
      WriteSlice(expr.Arguments[1], expr.Arguments[2], lenMinus1);
      Write(" = ");
      WriteSlice(expr.Obj, expr.Arguments[0], lenMinus1);
    }

    public override void Library_ToString(CiMethodCall expr) {
      CiExpr lenMinus1 = new CiBinaryExpr(expr.Arguments[1], CiToken.Minus, new CiConstExpr(1));
      Write("pack('U*', ");
      WriteSlice(expr.Obj, expr.Arguments[0], lenMinus1);
      Write(')');
    }

    public override void Library_Clear(CiMethodCall expr) {
      Write('@');
      if (expr.Obj is CiVarAccess) Write(((CiVarAccess)expr.Obj).Var.Name);
      else {
        Write('{');
        Translate(expr.Obj);
        Write('}');
      }
      Write(" = (0) x ");
      Write(((CiArrayStorageType)expr.Obj.Type).Length);
    }
    #endregion
  }
}

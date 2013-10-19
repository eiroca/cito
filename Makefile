prefix := /usr/local
srcdir := $(dir $(lastword $(MAKEFILE_LIST)))
CSC := $(if $(WINDIR),c:/Windows/Microsoft.NET/Framework/v3.5/csc.exe,gmcs)
MONO := $(if $(WINDIR),,mono)
ASCIIDOC = asciidoc -o - $(1) $< | xmllint -o $@ -
SEVENZIP = 7z a -mx=9 -bd

CILIB = $(addprefix $(srcdir)/CiLib/,CiDocLexer.cs CiDocParser.cs CiLexer.cs CiMacroProcessor.cs CiParser.cs CiResolver.cs CiTree.cs SymbolTable.cs) 
CIGEN = $(addprefix $(srcdir)/CiLib/,ProjectHelper.cs BaseGenerator.cs DelegatedGenerator.cs SourceGenerator.cs GenAs.cs GenD.cs GenC.cs GenC89.cs GenCs.cs GenJava.cs GenJs.cs GenJsWithTypedArrays.cs GenPas.cs GenPerl5.cs GenPerl58.cs GenPerl510.cs GenPHP.cs)

VERSION := 0.5.0
MAKEFLAGS = -r

all: cito.exe cipad.exe 

cito.exe: $(addprefix $(srcdir)/CiTo/,CiTo.cs Properties/AssemblyInfo.cs) $(CILIB) $(CIGEN)
	$(CSC) -nologo -out:$@ -o+ $^

cipad.exe: $(addprefix $(srcdir)/CiPAD/,Properties/AssemblyInfo.cs CiPad.cs) $(CILIB) $(CIGEN) $(addprefix $(srcdir)/logo/,ci-logo.ico)
	$(CSC) -nologo -out:$@ -o+ -t:winexe -win32icon:$(filter %.ico,$^) $(filter %.cs,$^) -r:System.Drawing.dll -r:System.Windows.Forms.dll

#civiewer.exe: $(addprefix $(srcdir)/CiViewer/,Program.cs MainWindow.cs IgeMacMenuGlobal.cs gtk-gui/generated.cs gtk-gui/MainWindow.cs Properties/AssemblyInfo.cs ci-logo.ico) $(CILIB) $(CIGEN)
#	$(CSC) -nologo -out:$@ -o+ -t:winexe -win32icon:$(filter %.ico,$^) $(filter %.cs,$^) -r:atk-sharp.dll -r:gdk/gdk-sharp.dll -r:glade-sharp.dll -r:glib-sharp.dll -r:gtk-sharp.dll -r:Mono.Posix.dll -r:pango-sharp.dll

$(srcdir)logo/ci-logo.png: $(srcdir)logo/ci-logo.svg
	convert -background none $< -gravity Center -resize "52x64!" -extent 64x64 -quality 95 $@

$(srcdir)logo/ci-logo.ico: $(srcdir)logo/ci-logo.svg
	convert -background none $< -gravity Center -resize "26x32!" -extent 32x32 $@

check: $(srcdir)sample/hello.ci cito.exe
	$(MONO) ./cito.exe          -o sample/hello.c $<
	$(MONO) ./cito.exe -l c99   -o sample/hello99.c $<
	$(MONO) ./cito.exe          -o sample/hello.java $<
	$(MONO) ./cito.exe          -o sample/hello.cs $<
	$(MONO) ./cito.exe          -o sample/hello.js $<
	$(MONO) ./cito.exe          -o sample/hello.as $<
	$(MONO) ./cito.exe          -o sample/hello.d $<
	$(MONO) ./cito.exe          -o sample/hello.pm $<
	$(MONO) ./cito.exe -l pm510 -o sample/hello5.10.pm $<
	$(MONO) ./cito.exe          -o sample/hello.pas $<
	$(MONO) ./cito.exe -l pm510 -o sample/hello.php $<

install: install-cito install-cipad

install-cito: cito.exe
	mkdir -p $(DESTDIR)$(prefix)/lib/cito $(DESTDIR)$(prefix)/bin
	cp $< $(DESTDIR)$(prefix)/lib/cito/cito.exe
	(echo '#!/bin/sh' && echo 'exec /usr/bin/mono $(DESTDIR)$(prefix)/lib/cito/cito.exe "$$@"') >$(DESTDIR)$(prefix)/bin/cito
	chmod 755 $(DESTDIR)$(prefix)/bin/cito

install-cipad: cipad.exe
	mkdir -p $(DESTDIR)$(prefix)/lib/cito $(DESTDIR)$(prefix)/bin
	cp $< $(DESTDIR)$(prefix)/lib/cito/cipad.exe
	(echo '#!/bin/sh' && echo 'exec /usr/bin/mono $(DESTDIR)$(prefix)/lib/cito/cipad.exe "$$@"') >$(DESTDIR)$(prefix)/bin/cipad
	chmod 755 $(DESTDIR)$(prefix)/bin/cipad

uninstall:
	$(RM) $(DESTDIR)$(prefix)/bin/cito $(DESTDIR)$(prefix)/lib/cito/cito.exe $(DESTDIR)$(prefix)/bin/cipad $(DESTDIR)$(prefix)/lib/cito/cipad.exe
	rmdir $(DESTDIR)$(prefix)/lib/cito

$(srcdir)README.html: $(srcdir)docs/README
	$(call ASCIIDOC,)

$(srcdir)INSTALL.html: $(srcdir)docs/INSTALL
	$(call ASCIIDOC,)

$(srcdir)ci.html: $(srcdir)docs/ci.txt
	$(call ASCIIDOC,-a toc)

www: index.html $(srcdir)INSTALL.html $(srcdir)ci.html

index.html: $(srcdir)docs/README
	$(call ASCIIDOC,-a www)

clean:
	$(RM) cito.exe cipad.exe 
	$(RM) sample/hello.c sample/hello.h sample/hello99.c sample/hello99.h sample/HelloCi.java sample/hello.cs sample/hello.js sample/HelloCi.as sample/hello.d sample/hello.pm sample/hello5.10.pm sample/hello.pas sample/hello.php 
	$(RM) index.html ci.html INSTALL.html README.html

dist: ../cito-$(VERSION)-bin.zip srcdist

../cito-$(VERSION)-bin.zip: cito.exe cipad.exe $(srcdir)COPYING $(srcdir)README.html $(srcdir)ci.html $(srcdir)sample/hello.ci
	$(RM) $@ && $(SEVENZIP) -tzip $@ $(^:%=./%)
# "./" makes 7z don't store paths in the archive

srcdist: $(addprefix $(srcdir),MANIFEST README.html INSTALL.html ci.html logo/ci-logo.ico)
	$(RM) ../cito-$(VERSION).tar.gz && tar -c --numeric-owner  -T MANIFEST  | $(SEVENZIP) -tgzip -si ../cito-$(VERSION).tar.gz

$(srcdir)MANIFEST:
	if test -e $(srcdir).git; then \
		(git ls-tree -r --name-only --full-tree master | grep -vF .gitignore \
			&& echo MANIFEST && echo README.html && echo INSTALL.html && echo ci.html && echo logo/ci-logo.ico) | sort -u >$@; \
	fi

version:
	@grep -H Version $(srcdir)CiTo/Properties/AssemblyInfo.cs
	@grep -H '"cito ' $(srcdir)CiTo/CiTo.cs

.PHONY: all check install install-cito install-cipad uninstall www clean srcdist $(srcdir)MANIFEST version

.DELETE_ON_ERROR:

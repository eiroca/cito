VERSION := 0.5.2
MAKEFLAGS = -r

prefix := /usr/local
srcdir := $(dir $(lastword $(MAKEFILE_LIST)))

CSC := $(if $(WINDIR),c:/Windows/Microsoft.NET/Framework/v3.5/csc.exe,gmcs)
MONO := $(if $(WINDIR),,mono)
ASCIIDOC = asciidoc -o - $(1) $< | xmllint -o $@ -
MAKEPDF = a2x -f pdf $< 
SEVENZIP = 7z a -mx=9 -bd

GTK_DIR = /Library/Frameworks/Mono.framework/Versions/Current/lib/mono/gtk-sharp-2.0/

CISVG = $(addprefix $(srcdir)res/,  ci-logo.svg)
CIICO = $(addprefix $(srcdir)res/,  ci-logo.ico)
CIPNG = $(addprefix $(srcdir)res/,  ci-logo.png)

CILIB = $(addprefix $(srcdir)CiLib/,CiDocLexer.cs CiDocParser.cs CiLexer.cs CiMacroProcessor.cs CiParser.cs CiResolver.cs CiTree.cs SymbolTable.cs) 
CIGEN = $(addprefix $(srcdir)CiLib/,ProjectHelper.cs BaseGenerator.cs DelegatedGenerator.cs SourceGenerator.cs GenAs.cs GenD.cs GenC.cs GenC89.cs GenCs.cs GenJava.cs GenJs.cs GenJsWithTypedArrays.cs GenPas.cs GenPerl5.cs GenPerl58.cs GenPerl510.cs GenPHP.cs)

CITO  = $(addprefix $(srcdir)CiTo/, Properties/AssemblyInfo.cs CiTo.cs )
CIPAD = $(addprefix $(srcdir)CiPAD/,Properties/AssemblyInfo.cs CiPad.cs)
CIVWR = $(addprefix $(srcdir)CiViewer/,Program.cs MainWindow.cs IgeMacMenuGlobal.cs gtk-gui/generated.cs gtk-gui/MainWindow.cs Properties/AssemblyInfo.cs)

SAMPLE_DIR = $(srcdir)sample

DOCS = $(srcdir)docs
WWW = $(addprefix $(srcdir)docs/,index.html install.html ci.html)
PDF = $(addprefix $(srcdir)docs/,readme.pdf install.pdf ci.pdf)

MANIFEST_FILE = $(addprefix $(srcdir),MANIFEST)

DIST_BIN = ../cito-$(VERSION)-bin.zip 
DIST_SRC = ../cito-$(VERSION)-src.tar.gz

all: cito.exe cipad.exe civiewer.exe

cito.exe: $(CITO) $(CILIB) $(CIGEN)
	$(CSC) -nologo -out:$@ -o+ $^

cipad.exe: $(CIPAD) $(CILIB) $(CIGEN) $(CIICO)
	$(CSC) -nologo -out:$@ -o+ -t:winexe -win32icon:$(filter %.ico,$^) $(filter %.cs,$^) -r:System.Drawing.dll -r:System.Windows.Forms.dll

civiewer.exe: $(CIVWR)  $(CILIB) $(CIGEN) $(CIICO)
	$(CSC) -nologo -out:$@ -o+ -t:winexe -win32icon:$(filter %.ico,$^) $(filter %.cs,$^) -r:$(GTK_DIR)atk-sharp.dll -r:$(GTK_DIR)/gdk-sharp.dll -r:$(GTK_DIR)/glade-sharp.dll -r:$(GTK_DIR)/glib-sharp.dll -r:$(GTK_DIR)gtk-sharp.dll -r:$(GTK_DIR)pango-sharp.dll -r:Mono.Posix.dll

check: $(SAMPLE_DIR)/hello.ci cito.exe
	$(MONO) ./cito.exe          -o $(SAMPLE_DIR)/hello.c $<
	$(MONO) ./cito.exe -l c99   -o $(SAMPLE_DIR)/hello99.c $<
	$(MONO) ./cito.exe          -o $(SAMPLE_DIR)/hello.java $<
	$(MONO) ./cito.exe          -o $(SAMPLE_DIR)/hello.cs $<
	$(MONO) ./cito.exe          -o $(SAMPLE_DIR)/hello.js $<
	$(MONO) ./cito.exe          -o $(SAMPLE_DIR)/hello.as $<
	$(MONO) ./cito.exe          -o $(SAMPLE_DIR)/hello.d $<
	$(MONO) ./cito.exe          -o $(SAMPLE_DIR)/hello.pm $<
	$(MONO) ./cito.exe -l pm510 -o $(SAMPLE_DIR)/hello5.10.pm $<
	$(MONO) ./cito.exe          -o $(SAMPLE_DIR)/hello.pas $<
	$(MONO) ./cito.exe -l pm510 -o $(SAMPLE_DIR)/hello.php $<

install: install-cito install-cipad install-civiewer

install-cito: cito.exe
	(echo '#!/bin/sh' && echo 'exec /usr/bin/mono $(DESTDIR)$(prefix)/lib/cito/cito.exe "$$@"') > cito.sh
	mkdir -p $(DESTDIR)$(prefix)/lib/cito $(DESTDIR)$(prefix)/bin
	cp $< $(DESTDIR)$(prefix)/lib/cito/cito.exe
	cp cito.sh $(DESTDIR)$(prefix)/bin/cito
	chmod 755 $(DESTDIR)$(prefix)/bin/cito
	rm cito.sh

install-cipad: cipad.exe
	(echo '#!/bin/sh' && echo 'exec /usr/bin/mono $(DESTDIR)$(prefix)/lib/cito/cipad.exe "$$@"') > cipad.sh
	mkdir -p $(DESTDIR)$(prefix)/lib/cito $(DESTDIR)$(prefix)/bin
	cp $< $(DESTDIR)$(prefix)/lib/cito/cipad.exe
	cp cipad.sh $(DESTDIR)$(prefix)/bin/cipad
	chmod 755 $(DESTDIR)$(prefix)/bin/cipad
	rm cipad.sh
	
install-civiewer: civiewer.exe
	(echo '#!/bin/bash' && echo 'export DYLD_FALLBACK_LIBRARY_PATH="/Library/Frameworks/Mono.framework/Versions/Current/lib:/usr/local/lib:/usr/lib"' && echo 'exec /usr/bin/mono $(DESTDIR)$(prefix)/lib/cito/civiewer.exe "$$@"') > civiewer.sh
	mkdir -p $(DESTDIR)$(prefix)/lib/cito $(DESTDIR)$(prefix)/bin
	cp $< $(DESTDIR)$(prefix)/lib/cito/civiewer.exe
	cp civiewer.sh $(DESTDIR)$(prefix)/bin/civiewer
	chmod 755 $(DESTDIR)$(prefix)/bin/civiewer
	rm civiewer.sh
	
uninstall:
	$(RM) $(DESTDIR)$(prefix)/bin/cito  $(DESTDIR)$(prefix)/lib/cito/cito.exe 
	$(RM) $(DESTDIR)$(prefix)/bin/cipad $(DESTDIR)$(prefix)/lib/cito/cipad.exe
	$(RM) $(DESTDIR)$(prefix)/bin/civiewer $(DESTDIR)$(prefix)/lib/cito/civiewer.exe
	rmdir $(DESTDIR)$(prefix)/lib/cito

res: $(CIPNG) $(CIICO)

$(CIPNG): $(CISVG)
	convert -background none $< -gravity Center -resize "52x64!" -extent 64x64 -quality 95 -strip $@

$(CIICO): $(CISVG)
	convert -background none $< -gravity Center -resize "26x32!" -extent 32x32 $@

www: $(WWW)

pdf: $(PDF)

docs: res www pdf

$(DOCS)/index.html: $(DOCS)/readme.txt
	$(call ASCIIDOC,-a www)

$(DOCS)/install.html: $(DOCS)/install.txt
	$(call ASCIIDOC,)

$(DOCS)/ci.html: $(DOCS)/ci.txt
	$(call ASCIIDOC,-a toc)

$(DOCS)/readme.pdf: $(DOCS)/readme.txt
	$(call MAKEPDF,)

$(DOCS)/install.pdf: $(DOCS)/install.txt
	$(call MAKEPDF,)

$(DOCS)/ci.pdf: $(DOCS)/ci.txt
	$(call MAKEPDF,)

clean:
	$(RM) $(DIST_BIN) $(DIST_SRC)
	$(RM) MANIFEST
	$(RM) cito.exe cipad.exe civiewer.exe
	$(RM) $(SAMPLE_DIR)/hello.c $(SAMPLE_DIR)/hello.h $(SAMPLE_DIR)/hello99.c $(SAMPLE_DIR)/hello99.h $(SAMPLE_DIR)/HelloCi.java $(SAMPLE_DIR)/hello.cs $(SAMPLE_DIR)/hello.js $(SAMPLE_DIR)/HelloCi.as $(SAMPLE_DIR)/hello.d $(SAMPLE_DIR)/hello.pm $(SAMPLE_DIR)/hello5.10.pm $(SAMPLE_DIR)/hello.pas $(SAMPLE_DIR)/hello.php 
	$(RM) $(CIICO) $(CIPNG)
	$(RM) $(DOCS)/index.html $(DOCS)/install.html $(DOCS)/ci.html
	$(RM) $(DOCS)/readme.pdf $(DOCS)/install.pdf  $(DOCS)/ci.pdf

dist: $(DIST_BIN) $(DIST_SRC)

$(DIST_BIN): cito.exe cipad.exe civiewer.exe $(srcdir)COPYING $(srcdir)README $(DOCS)/readme.pdf $(DOCS)/ci.pdf $(SAMPLE_DIR)/hello.ci
	$(RM) $@ && $(SEVENZIP) -tzip $@ $(^:%=./%)
# "./" makes 7z don't store paths in the archive

$(DIST_SRC): $(MANIFEST_FILE) $(WWW)
	$(RM) $@ && tar -c --numeric-owner  -T MANIFEST  | $(SEVENZIP) -tgzip -si $@

$(MANIFEST_FILE):
	if test -e $(srcdir).git; then \
		(git ls-tree -r --name-only --full-tree master | grep -vF .gitignore \
			&& echo MANIFEST \
			&& echo $(DOCS)/index.html && echo $(DOCS)/install.html && echo $(DOCS)/ci.html \
			) | sort -u >$@; \
	fi

version:
	@grep -H Version $(srcdir)CiTo/Properties/AssemblyInfo.cs
	@grep -H '"cito ' $(srcdir)CiTo/CiTo.cs

.PHONY: all check install install-cito install-cipad uninstall www clean srcdist $(srcdir)MANIFEST version

.DELETE_ON_ERROR:

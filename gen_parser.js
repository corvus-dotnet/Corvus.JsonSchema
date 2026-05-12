const fs = require('fs');
const path = $targetPath.replace(/\\\\/g, '\\\\');

let content = '';

function w(s) { content += s + '\n'; }

w('// <copyright file="YamlEventParser.cs" company="Endjin Limited">');
w('// Copyright (c) Endjin Limited. All rights reserved.');
w('// </copyright>');
w('');
w('using System.Buffers;');
w('using System.Diagnostics.CodeAnalysis;');
w('using System.Runtime.CompilerServices;');
w('');
w('#if STJ');
w('using Corvus.Yaml;');
w('using Corvus.Yaml.Internal;');
w('#else');
w('using Corvus.Text.Json.Yaml;');
w('using Corvus.Text.Json.Yaml.Internal;');
w('#endif');
w('');
w('#if STJ');
w('namespace Corvus.Yaml.Internal;');
w('#else');
w('namespace Corvus.Text.Json.Yaml.Internal;');
w('#endif');
w('');
w('/// <summary>');
w('/// A YAML event parser that fires callbacks for each YAML parse event.');
w('/// </summary>');
w('internal ref struct YamlEventParser');
w('{');
w('    private readonly ReadOnlySpan<byte> _buffer;');
w('    private readonly YamlEventCallback _callback;');
w('    private readonly YamlReaderOptions _options;');
w('    private int _pos;');
w('    private int _line;');
w('    private int _column;');
w('    private int _depth;');
w('    private int _flowMinIndent;');
w('    private bool _secondaryTagRedefined;');
w('    private int _secondaryTagPrefixStart;');
w('    private int _secondaryTagPrefixLength;');
w('    private byte[]? _scratchBuffer;');

fs.writeFileSync(path, content);
console.log('Header written: ' + content.length + ' chars');

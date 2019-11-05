"""\
A library of useful helper classes to the SAX classes, for the
convenience of application and driver writers.
"""

import os, urllib.parse, urllib.request, urllib.parse, urllib.error, types
import io
import sys
from . import handler
from . import xmlreader

try:
    _StringTypes = [bytes, str]
except AttributeError:
    _StringTypes = [bytes]

def __dict_replace(s, d):
    """Replace substrings of a string using a dictionary."""
    for key, value in list(d.items()):
        s = s.replace(key, value)
    return s

def escape(data, entities={}):
    """Escape &, <, and > in a string of data.

    You can escape other strings of data by passing a dictionary as
    the optional entities parameter.  The keys and values must all be
    strings; each key will be replaced with its corresponding value.
    """

    # must do ampersand first
    data = data.replace("&", "&amp;")
    data = data.replace(">", "&gt;")
    data = data.replace("<", "&lt;")
    if entities:
        data = __dict_replace(data, entities)
    return data

def unescape(data, entities={}):
    """Unescape &amp;, &lt;, and &gt; in a string of data.

    You can unescape other strings of data by passing a dictionary as
    the optional entities parameter.  The keys and values must all be
    strings; each key will be replaced with its corresponding value.
    """
    data = data.replace("&lt;", "<")
    data = data.replace("&gt;", ">")
    if entities:
        data = __dict_replace(data, entities)
    # must do ampersand last
    return data.replace("&amp;", "&")

def quoteattr(data, entities={}):
    """Escape and quote an attribute value.

    Escape &, <, and > in a string of data, then quote it for use as
    an attribute value.  The \" character will be escaped as well, if
    necessary.

    You can escape other strings of data by passing a dictionary as
    the optional entities parameter.  The keys and values must all be
    strings; each key will be replaced with its corresponding value.
    """
    entities = entities.copy()
    entities.update({'\n': '&#10;', '\r': '&#13;', '\t':'&#9;'})
    data = escape(data, entities)
    if '"' in data:
        if "'" in data:
            data = '"%s"' % data.replace('"', "&quot;")
        else:
            data = "'%s'" % data
    else:
        data = '"%s"' % data
    return data


def _gettextwriter(out, encoding):
    if out is None:
        import sys
        out = sys.stdout

    if isinstance(out, io.RawIOBase):
        buffer = io.BufferedIOBase(out)
        # Keep the original file open when the TextIOWrapper is
        # destroyed
        buffer.close = lambda: None
    else:
        # This is to handle passed objects that aren't in the
        # IOBase hierarchy, but just have a write method
        buffer = io.BufferedIOBase()
        buffer.writable = lambda: True
        buffer.write = out.write
        try:
            # TextIOWrapper uses this methods to determine
            # if BOM (for UTF-16, etc) should be added
            buffer.seekable = out.seekable
            buffer.tell = out.tell
        except AttributeError:
            pass
    # wrap a binary writer with TextIOWrapper
    return _UnbufferedTextIOWrapper(buffer, encoding=encoding,
                                   errors='xmlcharrefreplace',
                                   newline='\n')


class _UnbufferedTextIOWrapper(io.TextIOWrapper):
    def write(self, s):
        super(_UnbufferedTextIOWrapper, self).write(s)
        self.flush()


class XMLGenerator(handler.ContentHandler):

    def __init__(self, out=None, encoding="iso-8859-1"):
        handler.ContentHandler.__init__(self)
        out = _gettextwriter(out, encoding)
        self._write = out.write
        self._flush = out.flush
        self._ns_contexts = [{}] # contains uri -> prefix dicts
        self._current_context = self._ns_contexts[-1]
        self._undeclared_ns_maps = []
        self._encoding = encoding

    def _qname(self, name):
        """Builds a qualified name from a (ns_url, localname) pair"""
        if name[0]:
            # Per http://www.w3.org/XML/1998/namespace, The 'xml' prefix is
            # bound by definition to http://www.w3.org/XML/1998/namespace.  It
            # does not need to be declared and will not usually be found in
            # self._current_context.
            if 'http://www.w3.org/XML/1998/namespace' == name[0]:
                return 'xml:' + name[1]
            # The name is in a non-empty namespace
            prefix = self._current_context[name[0]]
            if prefix:
                # If it is not the default namespace, prepend the prefix
                return prefix + ":" + name[1]
        # Return the unqualified name
        return name[1]

    # ContentHandler methods

    def startDocument(self):
        self._write('<?xml version="1.0" encoding="%s"?>\n' %
                        self._encoding)

    def endDocument(self):
        self._flush()

    def startPrefixMapping(self, prefix, uri):
        self._ns_contexts.append(self._current_context.copy())
        self._current_context[uri] = prefix
        self._undeclared_ns_maps.append((prefix, uri))

    def endPrefixMapping(self, prefix):
        self._current_context = self._ns_contexts[-1]
        del self._ns_contexts[-1]

    def startElement(self, name, attrs):
        self._write('<' + name)
        for (name, value) in list(attrs.items()):
            self._write(' %s=%s' % (name, quoteattr(value)))
        self._write('>')

    def endElement(self, name):
        self._write('</%s>' % name)

    def startElementNS(self, name, qname, attrs):
        self._write('<' + self._qname(name))

        for prefix, uri in self._undeclared_ns_maps:
            if prefix:
                self._write(' xmlns:%s="%s"' % (prefix, uri))
            else:
                self._write(' xmlns="%s"' % uri)
        self._undeclared_ns_maps = []

        for (name, value) in list(attrs.items()):
            self._write(' %s=%s' % (self._qname(name), quoteattr(value)))
        self._write('>')

    def endElementNS(self, name, qname):
        self._write('</%s>' % self._qname(name))

    def characters(self, content):
        if not isinstance(content, str):
            content = str(content, self._encoding)
        self._write(escape(content))

    def ignorableWhitespace(self, content):
        if not isinstance(content, str):
            content = str(content, self._encoding)
        self._write(content)

    def processingInstruction(self, target, data):
        self._write('<?%s %s?>' % (target, data))


class XMLFilterBase(xmlreader.XMLReader):
    """This class is designed to sit between an XMLReader and the
    client application's event handlers.  By default, it does nothing
    but pass requests up to the reader and events on to the handlers
    unmodified, but subclasses can override specific methods to modify
    the event stream or the configuration requests as they pass
    through."""

    def __init__(self, parent = None):
        xmlreader.XMLReader.__init__(self)
        self._parent = parent

    # ErrorHandler methods

    def error(self, exception):
        self._err_handler.error(exception)

    def fatalError(self, exception):
        self._err_handler.fatalError(exception)

    def warning(self, exception):
        self._err_handler.warning(exception)

    # ContentHandler methods

    def setDocumentLocator(self, locator):
        self._cont_handler.setDocumentLocator(locator)

    def startDocument(self):
        self._cont_handler.startDocument()

    def endDocument(self):
        self._cont_handler.endDocument()

    def startPrefixMapping(self, prefix, uri):
        self._cont_handler.startPrefixMapping(prefix, uri)

    def endPrefixMapping(self, prefix):
        self._cont_handler.endPrefixMapping(prefix)

    def startElement(self, name, attrs):
        self._cont_handler.startElement(name, attrs)

    def endElement(self, name):
        self._cont_handler.endElement(name)

    def startElementNS(self, name, qname, attrs):
        self._cont_handler.startElementNS(name, qname, attrs)

    def endElementNS(self, name, qname):
        self._cont_handler.endElementNS(name, qname)

    def characters(self, content):
        self._cont_handler.characters(content)

    def ignorableWhitespace(self, chars):
        self._cont_handler.ignorableWhitespace(chars)

    def processingInstruction(self, target, data):
        self._cont_handler.processingInstruction(target, data)

    def skippedEntity(self, name):
        self._cont_handler.skippedEntity(name)

    # DTDHandler methods

    def notationDecl(self, name, publicId, systemId):
        self._dtd_handler.notationDecl(name, publicId, systemId)

    def unparsedEntityDecl(self, name, publicId, systemId, ndata):
        self._dtd_handler.unparsedEntityDecl(name, publicId, systemId, ndata)

    # EntityResolver methods

    def resolveEntity(self, publicId, systemId):
        return self._ent_handler.resolveEntity(publicId, systemId)

    # XMLReader methods

    def parse(self, source):
        self._parent.setContentHandler(self)
        self._parent.setErrorHandler(self)
        self._parent.setEntityResolver(self)
        self._parent.setDTDHandler(self)
        self._parent.parse(source)

    def setLocale(self, locale):
        self._parent.setLocale(locale)

    def getFeature(self, name):
        return self._parent.getFeature(name)

    def setFeature(self, name, state):
        self._parent.setFeature(name, state)

    def getProperty(self, name):
        return self._parent.getProperty(name)

    def setProperty(self, name, value):
        self._parent.setProperty(name, value)

    # XMLFilter methods

    def getParent(self):
        return self._parent

    def setParent(self, parent):
        self._parent = parent

# --- Utility functions

def prepare_input_source(source, base = ""):
    """This function takes an InputSource and an optional base URL and
    returns a fully resolved InputSource object ready for reading."""

    if type(source) in _StringTypes:
        source = xmlreader.InputSource(source)
    elif hasattr(source, "read"):
        f = source
        source = xmlreader.InputSource()
        source.setByteStream(f)
        if hasattr(f, "name"):
            source.setSystemId(f.name)

    if source.getByteStream() is None:
        try:
            sysid = source.getSystemId()
            basehead = os.path.dirname(os.path.normpath(base))
            encoding = sys.getfilesystemencoding()
            if isinstance(sysid, str):
                if not isinstance(basehead, str):
                    try:
                        basehead = basehead.decode(encoding)
                    except UnicodeDecodeError:
                        sysid = sysid.encode(encoding)
            else:
                if isinstance(basehead, str):
                    try:
                        sysid = sysid.decode(encoding)
                    except UnicodeDecodeError:
                        basehead = basehead.encode(encoding)
            sysidfilename = os.path.join(basehead, sysid)
            isfile = os.path.isfile(sysidfilename)
        except UnicodeError:
            isfile = False
        if isfile:
            source.setSystemId(sysidfilename)
            f = open(sysidfilename, "rb")
        else:
            source.setSystemId(urllib.parse.urljoin(base, source.getSystemId()))
            f = urllib.request.urlopen(source.getSystemId())

        source.setByteStream(f)

    return source

<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:msxml="urn:schemas-microsoft-com:xslt" xmlns:cd="urn:schemas-connect-digital">
  <xsl:output encoding="iso-8859-1" method="html" doctype-public="-//W3//DTD HTML 4.0 Transitional//EN"/>
  <xsl:param name="EmailTitle"/>
  <xsl:param name="RunDate"/>
  <xsl:param name="NewTags"/>
  <xsl:param name="FileName"/>
  <xsl:param name="StartDate"/>
  <xsl:param name="EndDate"/>
  
  <xsl:template match="/">
    <html>
      <head>
        <title></title>
      </head>
      <body>
        <xsl:apply-templates/>
      </body>
    </html>
  </xsl:template>

  <xsl:template match="/">

    <!--Parse the string variable into actualxml so we can walk the nodes.-->
    <!--<xsl:variable name="XMLBody" select="cd:parse($EmailXML)/*"/>-->
    
    <!--Calls New-Tags function to concatenate the values into a string for comparison.-->
    <xsl:variable name="NewTags">
      <xsl:call-template name="New-Tags">
      </xsl:call-template>
    </xsl:variable>

    <table width="100%" cellpadding="0" cellspacing="0" border="0" style="font-size:16px; font-family:helvetica-neue-light,Helvetica Neue,Helvetica,Arial,sans-serif;">
      <tr>
        <td align="center" style="padding:20px;">
          <table width="900" cellpadding="0" cellspacing="0" bgcolor="#ffffff" border="0" style="padding:20px; border-style: solid; border-color: gray; border-width: 1px">
            <tr>
              <td align="left" valign="top" colspan="2" rowspan="2">
                <table width="100%" cellpadding="0" cellspacing="0">
                  <tr>
                    <td>
                      <table width="100%">
                        <tr>
                          <td style="width:50;">
                            <img style="height:50; width:50;" src="https://keyhunter.azurewebsites.net/hd_logo_50x50.png"/>
                          </td>
                          <td align="center" valign="middle">
                            <div style="width:auto;font-size: 26px; color: #333; margin-bottom:5px;margin-top:20;">
                              <b>
                                <xsl:value-of select="$EmailTitle"/>
                              </b>
                            </div>
                            <i>
                              Generated on: <xsl:value-of select="$RunDate"/>
                            </i>
                          </td>
                          <td align="right" style="width:50;">
                            <img style="height:50; width:50;" src="https://keyhunter.azurewebsites.net/gcc_logo_50x50.png"/>
                          </td>
                        </tr>
                      </table>
                      <hr color="#F96302" width="100%"/>
                    </td>
                  </tr>
                  <tr>
                    <td align="center">
                      <table width="100%">
                        <tr>
                          <td colspan="2">                            
                            <div style="width:auto;font-size: 26px; color: #333; margin-bottom:5px;margin-top:20;">
                              Summary                            
                            </div>
                          </td>
                        </tr>
                        <tr>
                          <td>
                            Processed File:
                          </td>
                          <td>
                            <xsl:value-of select="$FileName"/>
                          </td>
                        </tr>
                        <tr>
                          <td>
                            Process Started:
                          </td>
                          <td>
                            <xsl:value-of select="$StartDate"/>
                          </td>
                        </tr>
                        <tr>
                          <td>
                            Processed Completed:
                          </td>
                          <td>
                            <xsl:value-of select="$EndDate"/>
                          </td>
                        </tr>
                        <tr>
                          <td colspan="2">                            
                            <div style="width:auto;font-size: 26px; color: #333; margin-top:20px;">
                              Created Tags                            
                            </div>
                          </td>
                        </tr>
                        <xsl:choose>
                          <xsl:when test="contains($NewTags, 'true') = 'true'">
                            <tr>
                              <td colspan="2">                            
                                <div style="width:auto;font-size: 16px; color: #333; margin-bottom:5px;">
                                  The following tags were added to Autobahn.                            
                                </div>
                              </td>
                            </tr>
                            <tr>
                              <td colspan="2">
                                <table style="border-collapse:collapse; margin-top:10px; width:100%" border="1" cellpadding="10" cellspacing="0" bordercolor="LightSteelBlue">
                                  <tr>
                                    <th bgcolor="#F96302" align="center" valign="center" style="color:#ffffff; font-size: 18px">
                                      Tag ID
                                    </th>
                                    <th bgcolor="#F96302" align="center" valign="center" style="color:#ffffff; font-size: 18px">
                                      Tag Value
                                    </th>
                                  </tr>
                                  <xsl:for-each select="*[position() = 1]/*">
                                    <!--<xsl:sort select="TagId"/>-->
                                    <xsl:if test="IsNew = 'true'">                                                                  
                                      <tr>
                                        <xsl:if test="position() mod 2 = 0">
                                          <xsl:attribute name="bgcolor">#F4F4F4</xsl:attribute>
                                          <xsl:attribute name="bordercolor">#F4F4F4</xsl:attribute>
                                        </xsl:if>
                                        <xsl:if test="position() mod 2 = 1">
                                          <xsl:attribute name="bgcolor">#FFFFFF</xsl:attribute>
                                          <xsl:attribute name="bordercolor">#FFFFFF</xsl:attribute>
                                        </xsl:if>
                                        <td align="center">
                                          <xsl:value-of select="TagId"/>
                                        </td>
                                        <td align="center">
                                          <xsl:value-of select="TagValue"/>
                                        </td>
                                      </tr>                                
                                    </xsl:if>
                                  </xsl:for-each>
                                </table>
                              </td>
                            </tr>
                          </xsl:when>
                          <xsl:otherwise>
                            <tr>
                              <td colspan="2">                            
                                <div style="width:auto;font-size: 16px; color: #333; margin-bottom:5px;">
                                  This import did not add new tags to Autobahn.                            
                                </div>
                              </td>
                            </tr>
                          </xsl:otherwise>
                        </xsl:choose>
                      </table>                    
                    </td>
                  </tr>
                </table>
              </td>
            </tr>            
          </table>
        </td>
      </tr>
    </table>
  </xsl:template>
  
  <!--Concatenate all 'IsNew' values into a string so we can do a compare to see
  if the string contains 'true'-->
  <xsl:template name="New-Tags">
    <xsl:for-each select="*[position() = 1]/*">
      <xsl:value-of select="IsNew"/>
    </xsl:for-each>
  </xsl:template>

  <!--Convert string to XML-->
  <msxml:script language="CSharp" implements-prefix="cd">
    <msxml:using namespace="System.IO"/>

    public XPathNodeIterator parse(string data)
    {
    if(data==null || data.Length==0)
    {
    data="&lt;Empty /&gt;";
    }

    StringReader stringReader = new StringReader(data);
    XPathDocument xPathDocument = new XPathDocument(stringReader);
    XPathNavigator xPathNavigator = xPathDocument.CreateNavigator();
    XPathExpression xPathExpression = xPathNavigator.Compile("/");
    XPathNodeIterator xPathNodeIterator = xPathNavigator.Select(xPathExpression);

    return xPathNodeIterator;
    }
  </msxml:script>
</xsl:stylesheet>
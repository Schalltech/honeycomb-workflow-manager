<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:param name="EmailTitle" />
  <xsl:param name="RunDate" />
  <xsl:param name="Summary" />
  <xsl:template match="/">
    <html>
      <head>
        <title>WFM: Automated Email</title>
      </head>
      <body>
        <xsl:apply-templates/>
      </body>
    </html>
  </xsl:template>

  <xsl:template match="/*">
    <table width="100%" cellpadding="0" cellspacing="0" border="0">
      <tr>
        <td align="center">
          <table width="800" cellpadding="0" cellspacing="0" border="0" bgcolor="whitesmoke">
            <tr>
              <td align="left" valign="top" colspan="2" rowspan="2">
                <table width="100%" bgcolor="#ffffff" cellpadding="0" cellspacing="0">
                  <tr>
                    <td style="border-left: solid; border-top: solid; border-right: solid; border-color: gray; border-width: 1px">
                      <table width="100%">
                        <tr>
                          <td>
                            <img src="http://logo.png" style="height:35px;"/>
                          </td>
                          <td align="center" valign="middle">
                            <font face="Verdana" size="4px">
                              <b>
                                <xsl:value-of select="$EmailTitle"/>
                              </b>
                            </font>
                            <br/>
                            <i>
                              <xsl:value-of select="$RunDate"/>
                            </i>
                          </td>
                          <td align="right">
                            
                          </td>
                        </tr>
                      </table>
                      <hr color="000000" width="95%"/>
                    </td>
                  </tr>
                  <tr>
                    <td align="center" style="border-left: solid; border-right: solid; border-color: gray; border-width: 1px">
                      <table height="600" width="90%" cellpadding="0" cellspacing="0" border="0">
                        <tr>
                          <td height="20"  align="center">
                            <xsl:value-of select="$Summary"/>
                          </td>
                        </tr>
                        <tr>
                          <td align="center" valign="top">
                            <table cellpadding="40" cellspacing="0" border="0">
                              <tr>
                                <td>
                                  <table style="border-collapse:collapse" border="1" cellpadding="10" cellspacing="0" bordercolor="LightSteelBlue">
                                    <xsl:for-each select="*[position() = 1]/*">
                                      <th bgcolor="#0099CC" bordercolor="#0099CC">
                                        <font color="#FFFFFF">
                                          <xsl:value-of select="local-name()"/>
                                        </font>
                                      </th>
                                    </xsl:for-each>
                                    <xsl:apply-templates/>
                                  </table>
                                </td>
                              </tr>
                            </table>

                            <br/>
                          </td>
                        </tr>
                      </table>
                    </td>
                    <tr>
                      <td style="border-left: solid; border-right: solid; border-color: gray; border-width: 1px">
                        <br/>
                      </td>
                    </tr>
                    <tr>
                      <td align="center"  style="border-left: solid; border-bottom: solid; border-right: solid; border-color: gray; border-width: 1px">
                        <br/>
                      </td>
                    </tr>
                  </tr>
                </table>
              </td>
              <td bgcolor="whitesmoke"></td>
            </tr>
            <tr>
              <td></td>
            </tr>
            <tr style="border: solid; border-color: black" height="12" >
              <td bgcolor="#ffffff" width="12"></td>
              <td bgcolor="whitesmoke" width="588"></td>
              <td width="10"></td>
            </tr>
          </table>
        </td>
      </tr>
    </table>
  </xsl:template>
  <xsl:template match="/*/*">
    <tr>
      <xsl:if test="position() mod 2 = 0">
        <xsl:attribute name="bgcolor">#FFFFFF</xsl:attribute>
        <xsl:attribute name="bordercolor">#FFFFFF</xsl:attribute>
      </xsl:if>
      <xsl:if test="position() mod 2 = 1">
        <xsl:attribute name="bgcolor">#F4F4F4</xsl:attribute>
        <xsl:attribute name="bordercolor">#F4F4F4</xsl:attribute>
      </xsl:if>
      <xsl:apply-templates/>
    </tr>
  </xsl:template>

  <xsl:template match="/*/*/*">
    <td align="center">
      <xsl:apply-templates/>
    </td>
  </xsl:template>

</xsl:stylesheet>
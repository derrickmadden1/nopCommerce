var _cp_access_token="xxxxx-xxxxx-xxxxx-xxxxx",_cp_postcode_field_width="65px",_cp_button_text="Find Address",_cp_button_class="",_cp_result_box_height=5,_cp_result_box_width="auto",_cp_busy_img_url="/images/crafty_clicks_busy.gif",_cp_hide_result=!0,_cp_show_only_for_uk=!1;if(!_cp_js_included){var _cp_js_included=1,_cp_instances=[],_cp_instance_idx=0,_cp_pl=["FLAT","SHOP","UNIT","BLOCK","STALL","SUITE","APARTMENT","MAISONETTE","HOUSE NUMBER"];function CraftyPostcodeCreate(){return _cp_instances[++_cp_instance_idx]=new CraftyPostcodeClass,_cp_instances[_cp_instance_idx].obj_idx=_cp_instance_idx,_cp_instances[_cp_instance_idx]}function _cp_sp(e){var t,s="";for(t=0;t<_cp_pl.length;t++)if((s=_cp_pl[t])==e.substr(0,s.length).toUpperCase())return e.substr(s.length);return""}function _cp_eh(e){for(var t="";t=e.shift();)if(!isNaN(parseInt(t)))return parseInt(t);return""}function _cp_kp(e){var t;e||(e=window.event),e.keyCode?t=e.keyCode:e.which&&(t=e.which),13==t&&this.onclick()}function CraftyPostcodeClass(){this.config={lookup_url:"pcls1.craftyclicks.co.uk/js/",access_token:"",basic_address:0,traditional_county:0,busy_img_url:"crafty_postcode_busy.gif",hide_result:0,org_uppercase:1,town_uppercase:1,county_uppercase:0,addr_uppercase:0,delimiter:", ",msg1:"Please wait while we find the address",err_msg1:"This postcode could not be found, please try again or enter your address manually",err_msg2:"This postcode is not valid, please try again or enter your address manually",err_msg3:"Unable to connect to address lookup server, please enter your address manually.",err_msg4:"An unexpected error occured, please enter your address manually.",res_autoselect:1,res_select_on_change:1,debug_mode:0,lookup_timeout:1e4,form:"",elements:"",max_width:"400px",max_lines:1,first_res_line:"---- please select your address ----",result_elem_id:"",on_result_ready:null,on_result_selected:null,on_error:null,pre_populate_common_address_parts:0,elem_company:"crafty_out_company",elem_house_num:"",elem_street1:"crafty_out_street1",elem_street2:"crafty_out_street2",elem_street3:"crafty_out_street3",elem_town:"crafty_out_town",elem_county:"crafty_out_county",elem_postcode:"crafty_in_out_postcode",elem_udprn:"crafty_out_udprn",single_res_autoselect:0,single_res_notice:"---- address found, see below ----",elem_search_house:"crafty_in_search_house",elem_search_street:"crafty_in_search_street",elem_search_town:"crafty_in_search_town",max_results:25,err_msg5:"The house name/number could not be found, please try again.",err_msg6:"No results found, please modify your search and try again.",err_msg7:"Too many results, please modify your search and try again.",err_msg9:"Please provide more data and try again.",err_msg8:"Trial account limit reached, please use AA11AA, AA11AB, AA11AD or AA11AE."},this.xmlhttp=null,this.res_arr=null,this.disp_arr=null,this.res_arr_idx=0,this.dummy_1st_line=0,this.cc=0,this.flexi_search=0,this.lookup_timeout=null,this.obj_name="",this.house_search=0,this.set=function(e,t){this.config[e]=t},this.res_clicked=function(e){this.cc++,this.res_selected(e)&&0!=this.config.hide_result&&(this.config.max_lines<=2&&1<this.cc||2<this.config.max_lines)&&(this.update_res(null),this.cc=0)},this.res_selected=function(e){if(1==this.dummy_1st_line){if(0==e)return 0;e--}return e=this.disp_arr[e].index,this.populate_form_fields(this.res_arr[e]),this.config.on_result_selected&&this.config.on_result_selected(e),1},this.populate_form_fields=function(e){for(var t=[],s=this.config.delimiter,r=0;r<8;r++)t[r]=this.get_elem(r);t[11]=this.get_elem(11),t[11]&&(t[11].value=e.udprn),t[0]&&(t[0]==t[1]&&""!=e.org?(t[1].value=e.org,t[1]=t[2],t[2]=t[3],t[3]=null):t[0].value=e.org);var i=e.housename2;""!=i&&""!=e.housename1&&(i+=s),i+=e.housename1;var o=e.housenumber;t[7]&&(""!=(t[7].value=i)&&""!=o&&(t[7].value+=s),t[7].value+=o,o=i="");var _=e.street1,a=e.street2;""!=o&&(""!=a?a=o+" "+a:_=""!=_?o+" "+_:o);var n=a+(""==a?"":""==_?"":s)+_,c=e.locality_dep,u=e.locality;""!=n&&parseInt(n)==n&&(""!=c?c=parseInt(n)+" "+c:u=parseInt(n)+" "+u,_=n="");var l=c+(""==c||""==u?"":s)+u,h=n+(""==n||""==l?"":s)+l;if(t[1]&&t[2]&&t[3])""!=e.pobox||""!=i?(""!=e.pobox?t[1].value=e.pobox:t[1].value=i,""==l?""==a?(t[2].value=_,t[3].value=""):(t[2].value=a,t[3].value=_):""==n?""==c?(t[2].value=u,t[3].value=""):(t[2].value=c,t[3].value=u):(t[2].value=n,t[3].value=l)):""==n?(""==c?(t[1].value=u,t[2].value=""):(t[1].value=c,t[2].value=u),t[3].value=""):""==l?(""==a?(t[1].value=_,t[2].value=""):(t[1].value=a,t[2].value=_),t[3].value=""):""==a?(t[1].value=_,""==c?(t[2].value=u,t[3].value=""):(t[2].value=c,t[3].value=u)):""==c?(t[1].value=a,t[2].value=_,t[3].value=u):n.length<l.length?(t[1].value=n,t[2].value=c,t[3].value=u):(t[1].value=a,t[2].value=_,t[3].value=l);else if(t[1]&&t[2])""!=e.pobox?(t[1].value=e.pobox,t[2].value=h):""!=i&&""!=n&&""!=l?i.length+n.length<n.length+l.length?(t[1].value=i+(""==i?"":s)+n,t[2].value=l):(t[1].value=i,t[2].value=n+(""==n?"":s)+l):""!=i&&""!=n?(t[1].value=i,t[2].value=n):""==i&&""!=n?""==l?""!=a?(t[1].value=a,t[2].value=_):(t[1].value=n,t[2].value=""):(t[1].value=n,t[2].value=l):""==n&&""!=i?(t[1].value=i,t[2].value=l):(t[1].value=l,t[2].value="");else{var d;d=t[1]?t[1]:t[2]?t[2]:t[3],""!=e.pobox?d.value=e.pobox+s+l:d.value=i+(""==i||""==h?"":s)+h}return t[4]&&(t[4].value=e.town),t[5]&&setSelectedValue(t[5],e.county),t[6]&&(t[6].value=e.postcode),1},this.show_busy=function(){var e=document.createElement("img"),t=document.createAttribute("src");t.value=this.config.busy_img_url,e.setAttributeNode(t),(t=document.createAttribute("title")).value=this.config.msg1,e.setAttributeNode(t),this.update_res(e)},this.disp_err=function(e,t){var s=null,r="";if(""!=e){switch(e){case"0001":r=this.config.err_msg1;break;case"0002":r=this.config.err_msg2;break;case"9001":r=this.config.err_msg3;break;case"0003":r=this.config.err_msg9;break;case"0004":r=this.config.err_msg6;break;case"0005":r=this.config.err_msg7;break;case"7001":r=this.config.err_msg8;break;default:r="("+e+") "+this.config.err_msg4}if(this.config.debug_mode){var i="";switch(e){case"8000":i=" :: No Access Token ";break;case"8001":i=" :: Invalid Token Format ";break;case"8002":i=" :: Invalid Token ";break;case"8003":i=" :: Out of Credits ";break;case"8004":i=" :: Restricted by rules ";break;case"8005":i=" :: Token suspended "}r+=i+" :: DBG :: "+t}s=document.createTextNode(r)}this.update_res(s),this.config.on_error&&this.config.on_error(r)},this.disp_err_msg=function(e){var t=null;""!=e&&(t=document.createTextNode(e)),this.update_res(t),this.config.on_error&&this.config.on_error(e)},this.display_res_line=function(e,t){var s=document.getElementById("crafty_postcode_lookup_result_option"+this.obj_idx),r=document.createElement("option");if(r.appendChild(document.createTextNode(e)),-1==t&&r.setAttribute("selected","selected"),null!=s)s.appendChild(r);else{var i=document.createElement("select");i.id="crafty_postcode_lookup_result_option"+this.obj_idx,i.onclick=Function("_cp_instances["+this.obj_idx+"].res_clicked(this.selectedIndex);"),i.onkeypress=_cp_kp,0!=this.config.res_select_on_change&&(i.onchange=Function("_cp_instances["+this.obj_idx+"].res_selected(this.selectedIndex);")),this.config.max_width&&""!=this.config.max_width&&(i.style.width=this.config.max_width);var o=this.res_arr_idx;1==this.dummy_1st_line&&o++,"Microsoft Internet Explorer"==navigator.appName&&parseFloat(navigator.appVersion)<=4?i.size=0:o>=this.config.max_lines?i.size=this.config.max_lines:i.size=o,i.appendChild(r),this.update_res(i)}},this.update_res=function(e){this.lookup_timeout&&clearTimeout(this.lookup_timeout);try{if(document.getElementById){var t=document.getElementById(this.config.result_elem_id);if(t.hasChildNodes())for(;t.firstChild;)t.removeChild(t.firstChild);null!=e&&t.appendChild(e)}}catch(e){}},this.str_trim=function(e){for(var t=0,s=e.length-1;t<e.length&&" "==e[t];)t++;for(;t<s&&" "==e[s];)s-=1;return e.substring(t,s+1)},this.cp_uc=function(e){if("PC"==e||"UK"==e||"EU"==e)return e;for(var t="",s=1,r=0,i=0;i<e.length;i++)-1!="ABCDEFGHIJKLMNOPQRSTUVWXYZ".indexOf(e.charAt(i))?s||r?(t+=e.charAt(i),s=0):t+=e.charAt(i).toLowerCase():(t+=e.charAt(i),i+2>=e.length&&"'"==e.charAt(i)?s=0:"("==e.charAt(i)?(close_idx=e.indexOf(")",i+1),i+3<close_idx?(r=0,s=1):r=1):")"==e.charAt(i)?(r=0,s=1):"-"==e.charAt(i)?(close_idx=e.indexOf("-",i+1),-1!=close_idx&&i+3>=close_idx||i+3>=e.length?s=r=0:(r=0,s=1)):s=i+2<e.length&&"0"<=e.charAt(i)&&e.charAt(i)<="9"?0:1);return t},this.leading_caps=function(e,t){if(0!=t||e.length<2)return e;for(var s="",r=e.split(" "),i=0;i<r.length;i++){var o=this.str_trim(r[i]);""!=o&&(""!=s&&(s+=" "),s+=this.cp_uc(o))}return s},this.new_res_line=function(){var e=[];return e.org="",e.housename1="",e.housename2="",e.pobox="",e.housenumber="",e.street1="",e.street2="",e.locality_dep="",e.locality="",e.town="",e.county="",e.postcode="",e.udprn="",e},this.res_arr_compare=function(e,t){if(e.match_quality>t.match_quality)return 1;if(e.match_quality<t.match_quality)return-1;if(e.street1>t.street1)return 1;if(e.street1<t.street1)return-1;if(e.street2>t.street2)return 1;if(e.street2<t.street2)return-1;var s,r;if(s=""==e.housenumber?_cp_eh(Array(e.housename1,e.housename2)):parseInt(e.housenumber),r=""==t.housenumber?_cp_eh(Array(t.housename1,t.housename2)):parseInt(t.housenumber),""==s&&""!=r)return 1;if(""!=s&&""==r)return-1;if(r<s)return 1;if(s<r)return-1;var i=_cp_sp(e.housename1);isNaN(parseInt(i))||(i=parseInt(i));var o=_cp_sp(t.housename1);if(isNaN(parseInt(o))||(o=parseInt(o)),o<i)return 1;if(i<o)return-1;i=_cp_sp(e.housename2);isNaN(parseInt(i))||(i=parseInt(i));o=_cp_sp(t.housename2);return isNaN(parseInt(o))||(o=parseInt(o)),o<i?1:i<o?-1:(i=e.housename2+e.housename1,(o=t.housename2+t.housename1)<i?1:i<o?-1:e.org>t.org?1:e.org<t.org?-1:1)},this.disp_res_arr=function(){this.res_arr=this.res_arr.sort(this.res_arr_compare),0!=this.config.res_autoselect&&this.populate_form_fields(this.res_arr[0]);var e=this.config.delimiter;this.disp_arr=[];for(var t=0;t<this.res_arr_idx;t++){var s=this.res_arr[t],r=s.org+(""!=s.org?e:"")+s.housename2+(""!=s.housename2?e:"")+s.housename1+(""!=s.housename1?e:"")+s.pobox+(""!=s.pobox?e:"")+s.housenumber+(""!=s.housenumber?" ":"")+s.street2+(""!=s.street2?e:"")+s.street1+(""!=s.street1?e:"")+s.locality_dep+(""!=s.locality_dep?e:"")+s.locality+(""!=s.locality?e:"")+s.town;this.flexi_search&&(r+=e+s.postcode);var i=[];i.index=t,i.str=r,this.disp_arr[t]=i}this.dummy_1st_line=0,""!=this.config.first_res_line&&(this.dummy_1st_line=1,this.display_res_line(this.config.first_res_line,-1));for(t=0;t<this.res_arr_idx;t++)this.display_res_line(this.disp_arr[t].str,t);if(this.config.pre_populate_common_address_parts){var o=this.new_res_line();o.org=this.res_arr[0].org,o.housename1=this.res_arr[0].housename1,o.housename2=this.res_arr[0].housename2,o.pobox=this.res_arr[0].pobox,o.housenumber=this.res_arr[0].housenumber,o.street1=this.res_arr[0].street1,o.street2=this.res_arr[0].street2,o.locality_dep=this.res_arr[0].locality_dep,o.locality=this.res_arr[0].locality,o.town=this.res_arr[0].town,o.county=this.res_arr[0].county,o.postcode=this.res_arr[0].postcode,o.udprn=this.res_arr[0].udprn;for(t=1;t<this.res_arr_idx;t++)this.res_arr[t].org!=o.org&&(o.org=""),this.res_arr[t].housename2!=o.housename2&&(o.housename2=""),this.res_arr[t].housename1!=o.housename1&&(o.housename1=""),this.res_arr[t].pobox!=o.pobox&&(o.pobox=""),this.res_arr[t].housenumber!=o.housenumber&&(o.housenumber=""),this.res_arr[t].street1!=o.street1&&(o.street1=""),this.res_arr[t].street2!=o.street2&&(o.street2=""),this.res_arr[t].locality_dep!=o.locality_dep&&(o.locality_dep=""),this.res_arr[t].locality!=o.locality&&(o.locality=""),this.res_arr[t].town!=o.town&&(o.town=""),this.res_arr[t].county!=o.county&&(o.county=""),this.res_arr[t].postcode!=o.postcode&&(o.postcode=""),this.res_arr[t].udprn!=o.udprn&&(o.udprn="");this.populate_form_fields(o)}},this.get_elem=function(e){var t="",s=null;if(""!=this.config.elements)t=this.config.elements.split(",")[e];else switch(e){case 0:t=this.config.elem_company;break;case 1:t=this.config.elem_street1;break;case 2:t=this.config.elem_street2;break;case 3:t=this.config.elem_street3;break;case 4:t=this.config.elem_town;break;case 5:t=this.config.elem_county;break;case 6:default:t=this.config.elem_postcode;break;case 7:t=this.config.elem_house_num;break;case 8:t=this.config.elem_search_house;break;case 9:t=this.config.elem_search_street;break;case 10:t=this.config.elem_search_town;break;case 11:t=this.config.elem_udprn}return""!=t&&(""!=this.config.form?s=document.forms[this.config.form].elements[t]:document.getElementById&&(s=document.getElementById(t))),s},this.doHouseSearch=function(){var e=this.get_elem(8);e&&0<e.value.length&&(this.house_search=1),this.doLookup()},this.doLookup=function(){this.xmlhttp=null;var e=this.get_elem(6),t=null;e&&(this.show_busy(),this.lookup_timeout=setTimeout("_cp_instances["+this.obj_idx+"].lookup_timeout_err()",this.config.lookup_timeout),t=this.validate_pc(e.value)),null!=t?this.direct_xml_fetch(0,t):this.disp_err("0002","invalid postcode format")},this.flexiSearch=function(){this.xmlhttp=null;var e="";this.get_elem(8)&&""!=this.get_elem(8).value&&(e+="&search_house="+this.get_elem(8).value),this.get_elem(9)&&""!=this.get_elem(9).value&&(e+="&search_street="+this.get_elem(9).value),this.get_elem(10)&&""!=this.get_elem(10).value&&(e+="&search_town="+this.get_elem(10).value),""!=e?(this.show_busy(),this.lookup_timeout=setTimeout("_cp_instances["+this.obj_idx+"].lookup_timeout_err()",this.config.lookup_timeout),this.direct_xml_fetch(1,e)):this.disp_err("0003","search string too short")},this.validate_pc=function(e){for(var t="";e=(t=e).replace(/[^A-Za-z0-9]/,""),t!=e;);if((t=e.toUpperCase()).length<=7&&5<=t.length){var s=t.substring(t.length-3,t.length),r=t.substring(0,t.length-3);if(1==/[CIKMOV]/.test(s))return null;if("0"<=s.charAt(0)&&s.charAt(0)<="9"&&"A"<=s.charAt(1)&&s.charAt(1)<="Z"&&"A"<=s.charAt(2)&&s.charAt(2)<="Z")switch(r.length){case 2:if("A"<=r.charAt(0)&&r.charAt(0)<="Z"&&"0"<=r.charAt(1)&&r.charAt(1)<="9")return t;break;case 3:if("A"<=r.charAt(0)&&r.charAt(0)<="Z"){if("0"<=r.charAt(1)&&r.charAt(1)<="9"&&"0"<=r.charAt(2)&&r.charAt(2)<="9")return t;if("A"<=r.charAt(1)&&r.charAt(1)<="Z"&&"0"<=r.charAt(2)&&r.charAt(2)<="9")return t;if("0"<=r.charAt(1)&&r.charAt(1)<="9"&&"A"<=r.charAt(2)&&r.charAt(2)<="Z")return t}break;case 4:if("A"<=r.charAt(0)&&r.charAt(0)<="Z"&&"A"<=r.charAt(1)&&r.charAt(1)<="Z"&&"0"<=r.charAt(2)&&r.charAt(2)<="9"){if("0"<=r.charAt(3)&&r.charAt(3)<="9")return t;if("A"<=r.charAt(3)&&r.charAt(3)<="Z")return t}}}return null},this.direct_xml_fetch=function(e,t){try{var s=document.getElementById(this.config.result_elem_id),r="";if(r="https:"==document.location.protocol?"https://":"http://",0==e)r+=this.config.lookup_url,this.config.basic_address?r+="basicaddress":r+="rapidaddress",r+="?postcode="+t+"&callback=_cp_instances["+this.obj_idx+"].handle_js_response&callback_id=0";else{if(this.config.basic_address)return void this.disp_err("1207","BasicAddress can't be used for Flexi Search!");r+=this.config.lookup_url+"flexiaddress?callback=_cp_instances["+this.obj_idx+"].handle_js_response&callback_id=1",r+="&max_results="+this.config.max_results,r+=t}""!=this.config.access_token&&(r+="&key="+this.config.access_token);var i=document.createElement("script");i.src=encodeURI(r),i.type="text/javascript",s.appendChild(i)}catch(e){this.disp_err("1206",e)}},this.handle_js_response=function(e,t,s){if(t){if(this.res_arr=[],(this.res_arr_idx=0)==e){if(this.flexi_search=0,this.house_search&&null==(s=this.filter_data_by_house_name(s)))return void this.disp_err_msg(this.config.err_msg5);this.add_to_res_array(s)}else{this.flexi_search=1,this.res_arr.total_postcode_count=s.total_postcode_count,this.res_arr.total_thoroughfare_count=s.total_thoroughfare_count,this.res_arr.total_delivery_point_count=s.total_delivery_point_count;for(var r=1;r<=s.total_postcode_count;r++)this.add_to_res_array(s[r])}if(this.res_arr_idx){var i=!1;if(1==this.res_arr_idx&&this.config.single_res_autoselect){var o=null;""!=this.config.single_res_notice&&(o=document.createTextNode(this.config.single_res_notice)),this.update_res(o),this.populate_form_fields(this.res_arr[0]),i=!0}else this.disp_res_arr(),document.getElementById("crafty_postcode_lookup_result_option"+this.obj_idx).focus();if(0==e&&""!=s.postcode)this.get_elem(6).value=s.postcode;this.config.on_result_ready&&this.config.on_result_ready(),i&&this.config.on_result_selected&&this.config.on_result_selected(0)}else this.disp_err("1205","no result to display")}else{var _=s.error_code,a=s.error_msg;this.disp_err(_,a)}},this.add_to_res_array=function(e){for(var t=1;t<=e.thoroughfare_count;t++){var s=e[t].thoroughfare_name;""!=e[t].thoroughfare_descriptor&&(s+=" "+e[t].thoroughfare_descriptor),s=this.leading_caps(s,this.config.addr_uppercase);var r=e[t].dependent_thoroughfare_name;if(""!=e[t].dependent_thoroughfare_descriptor&&(r+=" "+e[t].dependent_thoroughfare_descriptor),r=this.leading_caps(r,this.config.addr_uppercase),"delivery_point_count"in e[t]&&0<e[t].delivery_point_count)for(var i=1;i<=e[t].delivery_point_count;i++){var o;(o=this.new_res_line()).street1=s,o.street2=r;var _=e[t][i];o.match_quality="match_quality"in _?_.match_quality:1,o.housenumber=_.building_number,o.housename2=this.leading_caps(_.sub_building_name,this.config.addr_uppercase),o.housename1=this.leading_caps(_.building_name,this.config.addr_uppercase),o.org=_.department_name,""!=o.org&&""!=_.organisation_name&&(o.org+=this.config.delimiter),o.org=this.leading_caps(o.org+_.organisation_name,this.config.org_uppercase),o.pobox=this.leading_caps(_.po_box_number,this.config.addr_uppercase),o.postcode=e.postcode,o.town=this.leading_caps(e.town,this.config.town_uppercase),o.locality=this.leading_caps(e.dependent_locality,this.config.addr_uppercase),o.locality_dep=this.leading_caps(e.double_dependent_locality,this.config.addr_uppercase),this.config.traditional_county?o.county=this.leading_caps(e.traditional_county,this.config.county_uppercase):o.county=this.leading_caps(e.postal_county,this.config.county_uppercase),o.udprn=_.udprn,this.res_arr[this.res_arr_idx]=o,this.res_arr_idx++}else(o=this.new_res_line()).street1=s,o.street2=r,o.postcode=e.postcode,o.town=this.leading_caps(e.town,this.config.town_uppercase),o.locality=this.leading_caps(e.dependent_locality,this.config.addr_uppercase),o.locality_dep=this.leading_caps(e.double_dependent_locality,this.config.addr_uppercase),this.config.traditional_county?o.county=this.leading_caps(e.traditional_county,this.config.county_uppercase):o.county=this.leading_caps(e.postal_county,this.config.county_uppercase),o.match_quality=2,this.res_arr[this.res_arr_idx]=o,this.res_arr_idx++}},this.filter_data_by_house_name=function(e){var t=this.get_elem(8);if(!t||!t.value.length)return e;var s=t.value.toUpperCase(),r=-1;parseInt(s)==s&&(r=parseInt(s));for(var i=" "+s,o=[],_=1,a=0,n=1;n<=e.thoroughfare_count;n++){o[_]=[],a=0;for(var c=1;c<=e[n].delivery_point_count;c++){var u=e[n][c];-1==(" "+u.sub_building_name+" "+u.building_name+" ").indexOf(i)&&r!=parseInt(u.building_number)||(a++,o[_][a]=[],o[_][a].building_number=u.building_number,o[_][a].sub_building_name=u.sub_building_name,o[_][a].building_name=u.building_name,o[_][a].department_name=u.department_name,o[_][a].organisation_name=u.organisation_name,o[_][a].po_box_number=u.po_box_number,o[_][a].udprn=u.udprn)}a&&(o[_].delivery_point_count=a,o[_].thoroughfare_name=e[n].thoroughfare_name,o[_].thoroughfare_descriptor=e[n].thoroughfare_descriptor,o[_].dependent_thoroughfare_name=e[n].dependent_thoroughfare_name,o[_].dependent_thoroughfare_descriptor=e[n].dependent_thoroughfare_descriptor,_++)}return 1<_?(o.thoroughfare_count=_-1,o.town=e.town,o.dependent_locality=e.dependent_locality,o.double_dependent_locality=e.double_dependent_locality,o.traditional_county=e.traditional_county,o.postal_county=e.postal_county,o.postcode=e.postcode,o):null},this.lookup_timeout_err=function(){this.disp_err("9001","Internal Timeout after "+this.config.lookup_timeout+"ms")}}}function _cp_add_lookup(e,t,s){CraftyPostcodeCreate();for(var r=jQuery("#"+t+s[0]).closest("div"),i=7;0<i;i--)0<jQuery("#"+t+s[i]).closest("div").length&&r.after(jQuery("#"+t+s[i]).closest("div"));(r=jQuery("#"+t+s[1])).width(_cp_postcode_field_width),r.after('&nbsp;&nbsp;<button class="'+_cp_button_class+'" id="lookup_button'+e+'" type="button" onclick="_cp_do_lookup('+e+')">'+_cp_button_text+"</button>"),(r=jQuery("#"+t+s[1]).closest("div")).after('<div class="inputs _cp_result_tr_'+e+'"><label>&nbsp;</label><span id="crafty_postcode_result_display_'+e+'"></span></div>'),jQuery("#"+t+s[0]).closest("div").addClass("_cp_country_tr_"+e),jQuery("#"+t+s[1]).closest("div").addClass("_cp_postcode_tr_"+e),0<jQuery("#"+t+s[7]).length?jQuery("#"+t+s[7]).closest("div").addClass("_cp_county_tr_"+e):jQuery("#"+t+s[6]).closest("div").addClass("_cp_county_tr_"+e),_cp_instances[e].set("access_token",_cp_access_token),_cp_instances[e].set("res_autoselect","0"),_cp_instances[e].set("result_elem_id","crafty_postcode_result_display_"+e),_cp_instances[e].set("form",""),""!=s[2]?_cp_instances[e].set("elem_company",t+s[2]):_cp_instances[e].set("elem_company",t+s[3]),_cp_instances[e].set("elem_street1",t+s[3]),_cp_instances[e].set("elem_street2",t+s[4]),_cp_instances[e].set("elem_street3",t+s[5]),_cp_instances[e].set("elem_town",t+s[6]),_cp_instances[e].set("elem_county",t+s[7]),_cp_instances[e].set("elem_postcode",t+s[1]),_cp_instances[e].set("single_res_autoselect",1),_cp_instances[e].set("max_width",_cp_result_box_width),1<_cp_result_box_height?(_cp_instances[e].set("first_res_line",""),_cp_instances[e].set("max_lines",_cp_result_box_height)):(_cp_instances[e].set("first_res_line","----- please select your address ----"),_cp_instances[e].set("max_lines",1)),_cp_instances[e].set("busy_img_url",""),_cp_hide_result&&_cp_instances[e].set("hide_result",1),_cp_instances[e].set("traditional_county",1),_cp_instances[e].set("on_result_ready",function(){_cp_lookup_complete(e)}),_cp_instances[e].set("on_error",function(){_cp_lookup_complete(e)}),_cp_show_only_for_uk&&(jQuery("#"+t+s[0]).attr("_cp_idx",e),jQuery("#"+t+s[0]).change(function(e){"80"==jQuery(this).val()?_cp_set_for_uk(jQuery(this).attr("_cp_idx")):_cp_set_for_non_uk(jQuery(this).attr("_cp_idx"))}),jQuery("#"+t+s[0]).change())}function _cp_do_lookup(e){if(""!=_cp_busy_img_url)if(jQuery("#lookup_button"+e).hide(),0<jQuery("#busy_img"+e).length)jQuery("#busy_img"+e).show();else{var t=new Image;t.src=_cp_busy_img_url,t.id="busy_img"+e,jQuery("#lookup_button"+e).after(t)}_cp_instances[e].doLookup()}function _cp_lookup_complete(e){jQuery("#lookup_button"+e).show(),0<jQuery("#busy_img"+e).length&&jQuery("#busy_img"+e).hide()}function _cp_set_for_uk(e){jQuery("._cp_country_tr_"+e).after(jQuery("._cp_postcode_tr_"+e)),jQuery("#lookup_button"+e).show(),jQuery("._cp_result_tr_"+e).show()}function _cp_set_for_non_uk(e){jQuery("#lookup_button"+e).hide(),jQuery("._cp_result_tr_"+e).hide(),_cp_instances[e].update_res(null),jQuery("._cp_county_tr_"+e).after(jQuery("._cp_postcode_tr_"+e)),jQuery(".hidable_addr_lines"+e).show()}function setSelectedValue(e,t){for(var s=0;s<e.options.length;s++)if(e.options[s].text===t)return e.options[s].selected=!0,void e.onchange();e.nodeName;var r="Option '"+t+"' not found";throw""!=e.id?r=r+" in //"+e.nodeName.toLowerCase()+"[@id='"+e.id+"'].":""!=e.name?r=r+" in //"+e.nodeName.toLowerCase()+"[@name='"+e.name+"'].":r+=".",r}jQuery(document).ready(function(){var e=1;(0<jQuery("#BillingNewAddress_ZipPostalCode").length&&(_cp_add_lookup(e,"BillingNewAddress_",["CountryId","ZipPostalCode","Company","Address1","Address2","","City","StateProvinceId"]),e++),0<jQuery("#NewAddress_ZipPostalCode").length&&(console.log("found"),_cp_add_lookup(e,"NewAddress_",["CountryId","ZipPostalCode","Company","Address1","Address2","","City","StateProvinceId"]),e++),0<jQuery("#Address_ZipPostalCode").length&&(console.log("found"),_cp_add_lookup(e,"Address_",["CountryId","ZipPostalCode","Company","Address1","Address2","","City","StateProvinceId"]),e++),""!=_cp_busy_img_url)&&((new Image).src=_cp_busy_img_url)});
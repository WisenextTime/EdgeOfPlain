shader_type canvas_item;
uniform int TeamId = -1;

void fragment() {
	if(abs(COLOR.r*255.0-COLOR.b*255.0)<3.0 && abs(COLOR.r*255.0-COLOR.g*255.0)>3.0){
		float Deep = abs(COLOR.g - COLOR.r);
		switch(TeamId){
			case 0:
				{
					COLOR=vec4(0,Deep,0,COLOR.a);
					break;
				}
			case 1:
				{
					COLOR=vec4(Deep,0,0,COLOR.a);
					break;
				}
			case 2:
				{
					COLOR=vec4(0,0,Deep,COLOR.a);
					break;
				}
			case 3:
				{
					COLOR=vec4(Deep,Deep,0,COLOR.a);
					break;
				}
			case 4:
				{
					COLOR=vec4(0,Deep,Deep,COLOR.a);
					break;
				}
			case 5:
				{
					COLOR=vec4(Deep/2.0+0.5,Deep/2.0+0.5,Deep/2.0+0.5,COLOR.a);
					break;
				}
			case 6:
				{
					COLOR=vec4(Deep/8.0,Deep/8.0,Deep/8.0,COLOR.a);
					break;
				}
			case 7:
				{
					COLOR=vec4(Deep,0,Deep,COLOR.a);
					break;
				}
			case 8:
				{
					COLOR=vec4(Deep,Deep/2.0,0,COLOR.a);
					break;
				}
			case 9:
				{
					COLOR=vec4(Deep/2.0,0,Deep,COLOR.a);
					break;
				}
			default:
			{
				COLOR=vec4(Deep,Deep,Deep,COLOR.a);
				break;
			}
		}
	}
}
